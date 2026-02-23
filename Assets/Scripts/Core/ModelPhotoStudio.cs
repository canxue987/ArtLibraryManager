using UnityEngine;
using System.Collections;
using System.IO;
using System.Collections.Generic; 

namespace AssetLibrary.Core
{
    public class ModelPhotoStudio : MonoBehaviour
    {
        [Header("Settings")]
        public int Resolution = 512;
        public LayerMask PhotoLayer; 
        
        [Header("Framing")]
        [Range(0.1f, 3.0f)]
        public float ZoomMultiplier = 1.0f; 
        public Vector3 CameraOffset = new Vector3(0, 0.2f, 0);

        [Header("References")]
        public Camera PhotoCamera;
        public Light PhotoLight;

        private RenderTexture _renderTexture;
        private GameObject _currentPreviewModel;

        private Dictionary<Renderer, Material[]> _originalMaterialsCache = new Dictionary<Renderer, Material[]>();

        private void Awake()
        {
            if (PhotoCamera == null) PhotoCamera = GetComponentInChildren<Camera>();
            
            if (_renderTexture == null)
            {
                _renderTexture = new RenderTexture(Resolution, Resolution, 24);
                _renderTexture.name = "PreviewRT";
            }
            
            PhotoCamera.targetTexture = _renderTexture;
            PhotoCamera.enabled = true; 
            PhotoCamera.clearFlags = CameraClearFlags.SolidColor;
            PhotoCamera.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 1f);
            
            if (PhotoLayer.value == 0) PhotoLayer = -1;
            PhotoCamera.cullingMask = PhotoLayer;
        }

        public void ResizeRenderTexture(int newWidth, int newHeight)  
        {  
            if (_renderTexture != null)  
            {  
                if (_renderTexture.width == newWidth && _renderTexture.height == newHeight) return;  
                
                PhotoCamera.targetTexture = null;  
                if (Application.isPlaying) Destroy(_renderTexture);  
                else DestroyImmediate(_renderTexture);  
            }  

            _renderTexture = new RenderTexture(newWidth, newHeight, 24);  
            _renderTexture.name = "PreviewRT_Resized";  
            PhotoCamera.targetTexture = _renderTexture;  
        } 

        public bool HasModel() => _currentPreviewModel != null;

        public RenderTexture GetRenderTexture()
        {
            if (_renderTexture == null || !_renderTexture.IsCreated())
            {
                _renderTexture = new RenderTexture(Resolution, Resolution, 24);
                PhotoCamera.targetTexture = _renderTexture;
            }
            return _renderTexture;
        }

        public void LoadPreviewModel(GameObject model)
        {
            ClearPreviewModel(); 

            _currentPreviewModel = model;
            _currentPreviewModel.transform.SetParent(this.transform, false);
            _currentPreviewModel.transform.localPosition = Vector3.zero;
            _currentPreviewModel.transform.localRotation = Quaternion.Euler(0, 150, 0);

            int layerIndex = GetLayerIndexFromMask(PhotoLayer);
            SetLayerRecursively(_currentPreviewModel, layerIndex);

            FocusCameraOnObject(_currentPreviewModel);
        }

        // ============================================================================================
        // 【关键修改】提供两个重载方法，解决报错并支持多材质
        // ============================================================================================

        /// <summary>
        /// 重载1：接收完整 AssetMetaData (支持多材质) - 用于详情页
        /// </summary>
        public void ApplyMaterialsToPreview(AssetMetaData assetData, string libraryRoot, MonoBehaviour coroutineRunner, List<Coroutine> tracker = null, System.Action<float> onProgress = null)  
        {  
            if (_currentPreviewModel != null)  
            {  
                _originalMaterialsCache.Clear();  
                MaterialManager.ApplyMaterialBindings(_currentPreviewModel, assetData, libraryRoot, coroutineRunner, tracker, onProgress);  
            }  
        }  

        /// <summary>
        /// 重载2：接收单套 MaterialBindings (兼容旧代码/导入窗口)
        /// 内部将其包装为临时的 AssetMetaData
        /// </summary>
        public void ApplyMaterialsToPreview(MaterialBindings bindings, string libraryRoot, MonoBehaviour coroutineRunner, List<Coroutine> tracker = null, System.Action<float> onProgress = null)
        {
            if (_currentPreviewModel != null)
            {
                // 创建一个临时的 Meta 对象来包装单个 MaterialBindings
                // 这样 MaterialManager 就能正常工作了 (会走单材质回退逻辑)
                AssetMetaData tempMeta = new AssetMetaData();
                tempMeta.Materials = bindings;
                
                ApplyMaterialsToPreview(tempMeta, libraryRoot, coroutineRunner, tracker, onProgress);
            }
        }

        // ============================================================================================

        public void ShowClayMode()  
        {  
            if (_currentPreviewModel == null) return;  

            CacheOriginalMaterials();

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");  
            if (shader == null) shader = Shader.Find("Standard");  
            
            Material clayMat = new Material(shader);  
            clayMat.color = new Color(0.7f, 0.7f, 0.7f); 
            clayMat.SetFloat("_Smoothness", 0.2f);      

            Renderer[] renderers = _currentPreviewModel.GetComponentsInChildren<Renderer>();  
            foreach (var r in renderers)  
            {  
                Material[] sharedMats = new Material[r.sharedMaterials.Length];  
                for (int i = 0; i < sharedMats.Length; i++) sharedMats[i] = clayMat;  
                r.sharedMaterials = sharedMats;   
            }  
        }

        public bool ShowTexturedMode()
        {
            if (_currentPreviewModel == null) return false;
            if (_originalMaterialsCache.Count == 0) return false;

            Renderer[] renderers = _currentPreviewModel.GetComponentsInChildren<Renderer>();
            bool anyRestored = false;

            foreach (var r in renderers)
            {
                if (_originalMaterialsCache.TryGetValue(r, out Material[] originalMats))
                {
                    if (originalMats != null && originalMats.Length > 0 && originalMats[0] != null)
                    {
                        r.sharedMaterials = originalMats;
                        anyRestored = true;
                    }
                }
            }
            return anyRestored;
        }

        private void CacheOriginalMaterials()
        {
            if (_originalMaterialsCache.Count > 0) return;

            Renderer[] renderers = _currentPreviewModel.GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
            {
                _originalMaterialsCache[r] = r.sharedMaterials; 
            }
        }

        public GameObject GetCurrentPreviewModel() => _currentPreviewModel;

        public void ClearPreviewModel()
        {
            _originalMaterialsCache.Clear();

            if (_currentPreviewModel != null)
            {
                if (Application.isPlaying) Destroy(_currentPreviewModel);
                else DestroyImmediate(_currentPreviewModel);
                _currentPreviewModel = null;
            }
        }

        public void RotatePreview(float deltaX, float deltaY)  
        {  
            if (_currentPreviewModel == null) return;  
            _currentPreviewModel.transform.Rotate(Vector3.up, -deltaX, Space.World);  
        }  

        public void ZoomPreview(float scrollDelta)  
        {  
            if (_currentPreviewModel == null) return;  
            float sensitivity = 0.1f;  
            ZoomMultiplier -= scrollDelta * sensitivity;  
            ZoomMultiplier = Mathf.Clamp(ZoomMultiplier, 0.2f, 3.0f);  
            FocusCameraOnObject(_currentPreviewModel);  
        }

        public IEnumerator CaptureSnapshotAsync(GameObject model, string savePath, System.Action onComplete)  
        {  
            if (model == null) yield break;  

            int layerIndex = GetLayerIndexFromMask(PhotoLayer);  
            SetLayerRecursively(model, layerIndex);  
            FocusCameraOnObject(model);  

            yield return new WaitForEndOfFrame();  
            yield return new WaitForEndOfFrame();  

            PhotoCamera.Render();  

            RenderTexture.active = _renderTexture;  
            Texture2D result = new Texture2D(Resolution, Resolution, TextureFormat.RGB24, false);  
            result.ReadPixels(new Rect(0, 0, Resolution, Resolution), 0, 0);  
            result.Apply();  
            RenderTexture.active = null;  

            byte[] bytes = result.EncodeToJPG(90);  
            File.WriteAllBytes(savePath, bytes);  

            if (Application.isPlaying) Destroy(result);  
            else DestroyImmediate(result);  
            
            onComplete?.Invoke();  
        }  

        private void FocusCameraOnObject(GameObject obj)
        {
            Bounds bounds = CalculateBounds(obj);
            float objectSize = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z);
            float distance = objectSize / Mathf.Sin(PhotoCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            distance *= ZoomMultiplier;
            distance = Mathf.Max(distance, PhotoCamera.nearClipPlane + 0.5f);
            PhotoCamera.transform.position = bounds.center - Vector3.forward * distance + CameraOffset;
            PhotoCamera.transform.LookAt(bounds.center);
        }

        private Bounds CalculateBounds(GameObject obj)
        {
            Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(obj.transform.position, Vector3.one);
            Bounds b = renderers[0].bounds;
            foreach (Renderer r in renderers) b.Encapsulate(r.bounds);
            return b;
        }

        private void SetLayerRecursively(GameObject obj, int layerIndex)
        {
            obj.layer = layerIndex;
            foreach (Transform child in obj.transform) SetLayerRecursively(child.gameObject, layerIndex);
        }

        int GetLayerIndexFromMask(LayerMask mask)
        {
            int layer = 0;
            int value = mask.value;
            while(value > 1) { value >>= 1; layer++; }
            return layer;
        }
    }
}
