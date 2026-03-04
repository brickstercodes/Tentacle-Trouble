using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LoadDynamicImage : MonoBehaviour {

    private Material material;

    private void Awake() {
        material = GetComponent<MeshRenderer>().material;
    }

    private void Update() {
        if (Input.GetKeyDown(KeyCode.T)) {
            Texture2D texture2D = new Texture2D(1, 1, TextureFormat.ARGB32, false);
            texture2D.filterMode = FilterMode.Point;

            byte[] byteArray = File.ReadAllBytes(Application.dataPath + "/DrawPixels/PixelImage.png");
            texture2D.LoadImage(byteArray);

            material.mainTexture = texture2D;
        }
    }

}