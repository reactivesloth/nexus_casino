using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.UI;

namespace Code.Utility
{
    public static class ImageUtility
    {
        /// <summary>
        /// Кодирует NativeArray без ToArray-аллокации.
        /// </summary>
        public static NativeArray<byte>? Encode(NativeArray<byte> pixelData,
                                    GraphicsFormat format,
                                    uint width,
                                    uint height,
                                    bool asJpg = true,
                                    uint jpgQuality = 75)
        {
            if (!pixelData.IsCreated || width == 0 || height == 0) return null;

            return asJpg
                ? ImageConversion.EncodeNativeArrayToJPG(pixelData, format, width, height, jpgQuality)
                : ImageConversion.EncodeNativeArrayToPNG(pixelData, format, width, height);
            }

        /// <summary>
        /// Синхронный CPU Readback из RenderTexture.
        /// </summary>
        public static byte[] Encode(RenderTexture rt, bool asJpg = false, int jpgQuality = 75)
        {
            if (rt == null) return null;

            var prev = RenderTexture.active;
            RenderTexture.active = rt;

            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply(false, false);

            byte[] bytes = asJpg ? ImageConversion.EncodeToJPG(tex, jpgQuality)
                                 : ImageConversion.EncodeToPNG(tex);

            RenderTexture.active = prev;
            Object.Destroy(tex);
            return bytes;
        }

        public static Texture2D Decode(byte[] imageBytes)
        {
            if (imageBytes == null || imageBytes.Length == 0) return null;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(imageBytes, false);
            return tex;
        }

        public static Sprite CreateSpriteFromBytes(byte[] imageBytes)
        {
            var tex = Decode(imageBytes);
            if (tex == null) { Debug.LogWarning("Invalid image bytes."); return null; }
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }
        
        public static void AdjustAspect(RawImage target)
        {
            var texture = target.texture;
            var rectTransform = target.rectTransform;
            
            float textureRatio = (float)texture.width / texture.height;
            float parentWidth = rectTransform.parent.GetComponent<RectTransform>().rect.width;
            float parentHeight = rectTransform.parent.GetComponent<RectTransform>().rect.height;
            float parentRatio = parentWidth / parentHeight;

            if (textureRatio > parentRatio)
            {
                // Ограничиваем по ширине
                rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, parentWidth);
                rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, parentWidth / textureRatio);
            }
            else
            {
                // Ограничиваем по высоте
                rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, parentHeight);
                rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, parentHeight * textureRatio);
            }
        }
    }
}
