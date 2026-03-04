using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SplineFollower : MonoBehaviour {

    public enum MovementType {
        Normalized,
        Units
    }

    [SerializeField] private Spline spline = null;
    [SerializeField] private float speed = 1f;
    [SerializeField] private MovementType movementType = MovementType.Normalized;
    [SerializeField] private bool rotateObjectOnMove = true;
    [SerializeField] private bool loop = true;

    private float moveAmount;
    private float maxMoveAmount;

    private void Start() {
        switch (movementType) {
            default:
            case MovementType.Normalized:
                maxMoveAmount = 1f;
                break;
            case MovementType.Units:
                maxMoveAmount = spline.GetSplineLength();
                break;
        }
    }

    private void Update() {
        moveAmount = (moveAmount + (Time.deltaTime * speed));

        if (moveAmount > maxMoveAmount) {
            if (loop) {
                moveAmount = moveAmount % maxMoveAmount;
            } else {
                moveAmount = maxMoveAmount - .01f;
            }
        }

        switch (movementType) {
            default:
            case MovementType.Normalized:
                transform.position = spline.GetPositionAt(moveAmount);
                if (rotateObjectOnMove) {
                    transform.forward = spline.GetForwardAt(moveAmount);
                }
                break;
            case MovementType.Units:
                transform.position = spline.GetPositionAtUnits(moveAmount);
                if (rotateObjectOnMove) {
                    transform.forward = spline.GetForwardAtUnits(moveAmount);
                }
                break;
        }
    }

    public void SetMoveAmount(float moveAmount) {
        this.moveAmount = moveAmount;
    }

}
