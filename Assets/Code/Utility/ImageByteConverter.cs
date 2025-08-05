using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Code.Utility
{
    /// <summary>
    /// Утилита для кодирования NativeArray-данных или RenderTexture
    /// в PNG/JPG-массив байтов и обратного восстановления Texture2D.
    /// </summary>
    public static class ImageByteConverter
    {
        /// <summary>
        /// Кодирует данные GPU-чтения в PNG или JPG.
        /// </summary>
        public static byte[] Encode(NativeArray<byte> pixelData,
            GraphicsFormat format,
            uint width,
            uint height,
            bool asJpg = true,
            uint jpgQuality = 75)
        {
            if (asJpg)
                return ImageConversion.EncodeArrayToJPG(pixelData.ToArray(), format,
                    width, height, jpgQuality);
            return ImageConversion.EncodeArrayToPNG(pixelData.ToArray(), format, width, height);
        }

        /// <summary>
        /// Кодирует RenderTexture синхронно (менее производительно).
        /// </summary>
        public static byte[] Encode(RenderTexture rt,
            bool asJpg = false,
            int jpgQuality = 75)
        {
            var tmp = RenderTexture.active;
            RenderTexture.active = rt;

            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply(false, false);

            byte[] bytes = asJpg
                ? ImageConversion.EncodeToJPG(tex, jpgQuality)
                : ImageConversion.EncodeToPNG(tex);

            RenderTexture.active = tmp;
            UnityEngine.Object.Destroy(tex);
            return bytes;
        }

        /// <summary>
        /// Восстанавливает Texture2D из PNG/JPG массива.
        /// </summary>
        public static Texture2D Decode(byte[] imageBytes)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(imageBytes, false);
            return tex;
        }
        
        public static Sprite CreateSpriteFromBytes(byte[] imageBytes)
        {
            if (imageBytes == null || imageBytes.Length == 0)
            {
                Debug.LogWarning("Invalid image byte array.");
                return null;
            }

            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            bool isLoaded = texture.LoadImage(imageBytes);
            if (!isLoaded)
            {
                Debug.LogWarning("Failed to load image from bytes.");
                return null;
            }

            return Sprite.Create(
                texture,
                new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f)
            );
        }
    }
}