using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SphereMoveMouse : MonoBehaviour {


    private void Update() {
        transform.position = Mouse3D.GetMouseWorldPosition();
    }

}