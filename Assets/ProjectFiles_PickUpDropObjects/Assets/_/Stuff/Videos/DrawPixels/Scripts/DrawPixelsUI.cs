using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DrawPixels_Done {

    public class DrawPixelsUI : MonoBehaviour {

        [SerializeField] private Texture2D colorsTexture;
        [SerializeField] private Camera saveCamera;

        private Image selectedColorImage;

        private void Awake() {
            transform.Find("SmallBtn").GetComponent<Button>().onClick.AddListener(() => { DrawPixels.Instance.SetCursorSize(DrawPixels.CursorSize.Small); });
            transform.Find("MediumBtn").GetComponent<Button>().onClick.AddListener(() => { DrawPixels.Instance.SetCursorSize(DrawPixels.CursorSize.Medium); });
            transform.Find("LargeBtn").GetComponent<Button>().onClick.AddListener(() => { DrawPixels.Instance.SetCursorSize(DrawPixels.CursorSize.Large); });

            transform.Find("SaveBtn").GetComponent<Button>().onClick.AddListener(() => {
                DrawPixels.Instance.SaveImage((Texture2D texture2D) => {
                    transform.Find("RawImage").GetComponent<RawImage>().texture = texture2D;

                    byte[] byteArray = texture2D.EncodeToPNG();
                    System.IO.File.WriteAllBytes(Application.dataPath + "/DrawPixels/PixelImage.png", byteArray);
                });
                //SaveImageCamera(100, 100);
            });

            selectedColorImage = transform.Find("SelectedColor").GetComponent<Image>();
        }

        private void Start() {
            DrawPixels.Instance.OnColorChanged += DrawPixels_OnColorChanged;

            UpdateSelectedColor();
        }

        private void DrawPixels_OnColorChanged(object sender, System.EventArgs e) {
            UpdateSelectedColor();
        }

        private void UpdateSelectedColor() {
            Vector2 pixelCoordinates = DrawPixels.Instance.GetColorUV();
            pixelCoordinates.x *= colorsTexture.width;
            pixelCoordinates.y *= colorsTexture.height;
            selectedColorImage.color = colorsTexture.GetPixel((int)pixelCoordinates.x, (int)pixelCoordinates.y);
        }

        private void SaveImageCamera(int width, int height) {
            RenderTexture renderTexture = new RenderTexture(width, height, 16, RenderTextureFormat.ARGB32);
            saveCamera.targetTexture = renderTexture;
            saveCamera.enabled = true;
            saveCamera.Render();

            RenderTexture.active = renderTexture;
            Texture2D screenshotTexture = new Texture2D(width, height, TextureFormat.ARGB32, false);
            Rect rect = new Rect(0, 0, width, height);
            screenshotTexture.ReadPixels(rect, 0, 0);
            screenshotTexture.Apply();

            saveCamera.enabled = false;
            transform.Find("RawImage").GetComponent<RawImage>().texture = renderTexture;

            byte[] byteArray = screenshotTexture.EncodeToPNG();
            System.IO.File.WriteAllBytes(Application.dataPath + "/DrawPixels/PixelImage.png", byteArray);
        }

    }

}