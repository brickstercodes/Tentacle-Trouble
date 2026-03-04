using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using CodeMonkey.Utils;

public class SaveLoadUI : MonoBehaviour {

    [SerializeField] private Transform weapon1;
    [SerializeField] private Transform weapon2;
    [SerializeField] private Transform weapon3;
    [SerializeField] private Transform armor2;
    [SerializeField] private Transform armor3;
    [SerializeField] private Transform hat2;
    [SerializeField] private Transform hat3;


    private void Awake() {
        LoadSaveImage();
    }

    public void SetWeapon1() {
        weapon1.gameObject.SetActive(true);
        weapon2.gameObject.SetActive(false);
        weapon3.gameObject.SetActive(false);
    }

    public void SetWeapon2() {
        weapon1.gameObject.SetActive(false);
        weapon2.gameObject.SetActive(true);
        weapon3.gameObject.SetActive(false);
    }

    public void SetWeapon3() {
        weapon1.gameObject.SetActive(false);
        weapon2.gameObject.SetActive(false);
        weapon3.gameObject.SetActive(true);
    }


    public void SetArmor1() {
        armor2.gameObject.SetActive(false);
        armor3.gameObject.SetActive(false);
    }

    public void SetArmor2() {
        armor2.gameObject.SetActive(true);
        armor3.gameObject.SetActive(false);
    }

    public void SetArmor3() {
        armor2.gameObject.SetActive(false);
        armor3.gameObject.SetActive(true);
    }

    public void SetHat1() {
        hat2.gameObject.SetActive(false);
        hat3.gameObject.SetActive(false);
    }

    public void SetHat2() {
        hat2.gameObject.SetActive(true);
        hat3.gameObject.SetActive(false);
    }

    public void SetHat3() {
        hat2.gameObject.SetActive(false);
        hat3.gameObject.SetActive(true);
    }

    public void SaveData() {
        SaveFileScreenshotDemo.Instance.Save();
        FunctionTimer.Create(LoadSaveImage, .1f);
    }

    public void LoadData() {
        SaveFileScreenshotDemo.Instance.Load();
    }

    private void LoadSaveImage() {
        SaveFileScreenshotDemo.FileDataWithImage.Load(out SaveFileScreenshotDemo.SaveData saveData, out Texture2D screenshotTexture2D);

        //Texture2D texture2D = new Texture2D(1, 1, TextureFormat.ARGB32, false);
        //texture2D.LoadImage(System.IO.File.ReadAllBytes(Application.dataPath + "/SaveFileScreenshot/CameraScreenshot.png"));
        transform.Find("RawImage").GetComponent<RawImage>().texture = screenshotTexture2D;
    }

}