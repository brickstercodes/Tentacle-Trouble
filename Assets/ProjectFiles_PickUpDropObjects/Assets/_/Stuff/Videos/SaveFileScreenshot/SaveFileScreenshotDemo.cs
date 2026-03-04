using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

public class SaveFileScreenshotDemo : MonoBehaviour {

    public static SaveFileScreenshotDemo Instance { get; private set; }


    [SerializeField] private GameObject weapon1;
    [SerializeField] private GameObject weapon2;
    [SerializeField] private GameObject weapon3;
    [SerializeField] private GameObject hat2;
    [SerializeField] private GameObject hat3;
    [SerializeField] private GameObject armor2;
    [SerializeField] private GameObject armor3;

    [SerializeField] private Camera screenshotCamera;


    private Action<Texture2D> onScreenshotTaken;

    private void Awake() {
        Instance = this;
        RenderPipelineManager.endFrameRendering += RenderPipelineManager_endFrameRendering;
    }

    private void RenderPipelineManager_endFrameRendering(ScriptableRenderContext arg1, Camera[] arg2) {
        if (onScreenshotTaken != null) {
            RenderTexture renderTexture = screenshotCamera.targetTexture;

            Texture2D renderResult = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.ARGB32, false);
            Rect rect = new Rect(0, 0, renderTexture.width, renderTexture.height);
            renderResult.ReadPixels(rect, 0, 0);

            RenderTexture.ReleaseTemporary(renderTexture);
            screenshotCamera.targetTexture = null;

            onScreenshotTaken(renderResult);
            onScreenshotTaken = null;
        }
    }


    public class SaveData {

        public int hat;
        public int armor;
        public int weapon;

    }

    public class FileDataWithImage {

        [Serializable]
        public class Header {

            public int jsonByteSize;

        }

        public static void Save(string json, Texture2D screenshot) {
            byte[] jsonByteArray = Encoding.Unicode.GetBytes(json);
            byte[] screenshotByteArray = screenshot.EncodeToPNG();


            Header header = new Header {
                jsonByteSize = jsonByteArray.Length
            };
            string headerJson = JsonUtility.ToJson(header);
            byte[] headerJsonByteArray = Encoding.Unicode.GetBytes(headerJson);

            ushort headerSize = (ushort)headerJsonByteArray.Length;
            byte[] headerSizeByteArray = BitConverter.GetBytes(headerSize);

            List<byte> byteList = new List<byte>();
            byteList.AddRange(headerSizeByteArray);
            byteList.AddRange(headerJsonByteArray);
            byteList.AddRange(jsonByteArray);
            byteList.AddRange(screenshotByteArray);

            File.WriteAllBytes(Application.dataPath + "/SaveFileScreenshot/SaveFile.bytesave", byteList.ToArray());
        }

        public static void Load(out SaveData saveData, out Texture2D screenshotTexture2D) {
            byte[] byteArray = File.ReadAllBytes(Application.dataPath + "/SaveFileScreenshot/SaveFile.bytesave");
            List<byte> byteList = new List<byte>(byteArray);

            ushort headerSize = BitConverter.ToUInt16(new byte[] { byteArray[0], byteArray[1] }, 0);
            List<byte> headerByteList = byteList.GetRange(2, headerSize);
            string headerJson = Encoding.Unicode.GetString(headerByteList.ToArray());
            Header header = JsonUtility.FromJson<Header>(headerJson);

            List<byte> jsonByteList = byteList.GetRange(2 + headerSize, header.jsonByteSize);
            string gameDataJson = Encoding.Unicode.GetString(jsonByteList.ToArray());
            saveData = JsonUtility.FromJson<SaveData>(gameDataJson);

            int startIndex = 2 + headerSize + header.jsonByteSize;
            int endIndex = byteArray.Length - startIndex;
            List<byte> screenshotByteList = byteList.GetRange(startIndex, endIndex);
            screenshotTexture2D = new Texture2D(1, 1, TextureFormat.ARGB32, false);
            screenshotTexture2D.LoadImage(screenshotByteList.ToArray());
        }

    }


    public string GetSaveDataJSON() {
        SaveData saveData = new SaveData();

        if (weapon1.activeSelf) saveData.weapon = 1;
        if (weapon2.activeSelf) saveData.weapon = 2;
        if (weapon3.activeSelf) saveData.weapon = 3;

        saveData.hat = 1;
        if (hat2.activeSelf) saveData.hat = 2;
        if (hat3.activeSelf) saveData.hat = 3;

        saveData.armor = 1;
        if (armor2.activeSelf) saveData.armor = 2;
        if (armor3.activeSelf) saveData.armor = 3;

        string json = JsonUtility.ToJson(saveData);
        return json;
    }

    public void Save() {
        string SAVE_FOLDER = Application.dataPath;

        string json = GetSaveDataJSON();
        byte[] jsonByteArray = Encoding.Unicode.GetBytes(json);

        //File.WriteAllText(SAVE_FOLDER + "/SaveFileScreenshot/SaveFileGameData.save", json);

        TakeScreenshot(512, 512, (Texture2D screenshotTexture) => {
            byte[] screenshotByteArray = screenshotTexture.EncodeToPNG();

            List<byte> byteList = new List<byte>(jsonByteArray);
            byteList.AddRange(screenshotByteArray);

            //File.WriteAllBytes(Application.dataPath + "/SaveFileScreenshot/SaveFile.bytesave", byteList.ToArray());

            FileDataWithImage.Save(json, screenshotTexture);
            //File.WriteAllBytes(Application.dataPath + "/SaveFileScreenshot/CameraScreenshot.png", screenshotByteArray);
        });
    }

    public void Load() {
        FileDataWithImage.Load(out SaveData saveData, out Texture2D screenshotTexture2D);

        /*
        string SAVE_FOLDER = Application.dataPath;

        string json = File.ReadAllText(SAVE_FOLDER + "/SaveFileScreenshot/SaveFileGameData.save");

        byte[] byteArray = File.ReadAllBytes(SAVE_FOLDER + "/SaveFileScreenshot/SaveFileGameData.save");

        SaveData saveData = JsonUtility.FromJson<SaveData>(json);
        */

        weapon1.SetActive(saveData.weapon == 1);
        weapon2.SetActive(saveData.weapon == 2);
        weapon3.SetActive(saveData.weapon == 3);

        hat2.SetActive(saveData.hat == 2);
        hat3.SetActive(saveData.hat == 3);

        armor2.SetActive(saveData.armor == 2);
        armor3.SetActive(saveData.armor == 3);
    }

    private void TakeScreenshot(int width, int height, Action<Texture2D> onScreenshotTaken) {
        screenshotCamera.targetTexture = RenderTexture.GetTemporary(512, 512, 16);
        this.onScreenshotTaken = onScreenshotTaken;
    }
}