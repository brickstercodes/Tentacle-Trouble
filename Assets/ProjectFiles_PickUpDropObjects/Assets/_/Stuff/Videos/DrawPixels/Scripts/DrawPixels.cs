using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CodeMonkey.Utils;

namespace DrawPixels_Done {

    public class DrawPixels : MonoBehaviour {

        public static DrawPixels Instance { get; private set; }


        public event EventHandler OnColorChanged;

        public enum CursorSize {
            Small,
            Medium,
            Large
        }

        [SerializeField] private Texture2D colorsTexture;
        private Grid<PixelGridObject> grid;
        private float cellSize = 1f;
        private Vector2 colorUV;
        private CursorSize cursorSize;

        private void Awake() {
            Instance = this;

            grid = new Grid<PixelGridObject>(100, 100, cellSize, Vector3.zero, (Grid<PixelGridObject> g, int x, int y) => new PixelGridObject(g, x, y));
            colorUV = new Vector2(0, 0);
            cursorSize = CursorSize.Small;
        }

        private void Update() {
            if (Input.GetMouseButton(0)) {
                // Paint on grid
                Vector3 mouseWorldPosition = UtilsClass.GetMouseWorldPosition();
                int cursorSize = GetCursorSizeInt();
                for (int x = 0; x < cursorSize; x++) {
                    for (int y = 0; y < cursorSize; y++) {
                        Vector3 gridWorldPosition = mouseWorldPosition + new Vector3(x, y) * cellSize;
                        PixelGridObject pixelGridObject = grid.GetGridObject(gridWorldPosition);
                        if (pixelGridObject != null) {
                            pixelGridObject.SetColorUV(colorUV);
                        }
                    }
                }

                // Color picker
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit raycastHit, 999f)) {
                    colorUV = raycastHit.textureCoord;
                    OnColorChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public Grid<PixelGridObject> GetGrid() {
            return grid;
        }

        public Vector2 GetColorUV() {
            return colorUV;
        }

        public void SetCursorSize(CursorSize cursorSize) {
            this.cursorSize = cursorSize;
        }

        private int GetCursorSizeInt() {
            switch (cursorSize) {
                default:
                case CursorSize.Small: return 1;
                case CursorSize.Medium: return 3;
                case CursorSize.Large: return 7;
            }
        }

        public void SaveImage(Action<Texture2D> onSaveImage) {
            Texture2D texture2D = new Texture2D(grid.GetWidth(), grid.GetHeight(), TextureFormat.ARGB32, false);
            texture2D.filterMode = FilterMode.Point;

            for (int x = 0; x < grid.GetWidth(); x++) {
                for (int y = 0; y < grid.GetHeight(); y++) {
                    PixelGridObject gridObject = grid.GetGridObject(x, y);
                    Vector2 pixelCoordinates = gridObject.GetColorUV();
                    pixelCoordinates.x *= colorsTexture.width;
                    pixelCoordinates.y *= colorsTexture.height;
                    texture2D.SetPixel(x, y, colorsTexture.GetPixel((int)pixelCoordinates.x, (int)pixelCoordinates.y));
                }
            }

            texture2D.Apply();

            onSaveImage(texture2D);
        }


        public class PixelGridObject {

            private Grid<PixelGridObject> grid;
            private int x;
            private int y;
            private Vector2 colorUV;

            public PixelGridObject(Grid<PixelGridObject> grid, int x, int y) {
                this.grid = grid;
                this.x = x;
                this.y = y;
            }

            public void SetColorUV(Vector2 colorUV) {
                this.colorUV = colorUV;
                TriggerGridObjectChanged();
            }

            public Vector2 GetColorUV() {
                return colorUV;
            }

            private void TriggerGridObjectChanged() {
                grid.TriggerGridObjectChanged(x, y);
            }

            public override string ToString() {
                return colorUV.x.ToString();// x + "," + y;
            }

        }

    }

}