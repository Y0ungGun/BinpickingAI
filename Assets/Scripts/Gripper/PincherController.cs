using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum GripState { Fixed = 0, Opening = -1, Closing = 1 };

public class PincherController : MonoBehaviour
{
    public GameObject fingerA;
    public GameObject fingerB;

    PincherFingerController fingerAController;
    PincherFingerController fingerBController;

    // Grip - the extent to which the pincher is closed. 0: fully open, 1: fully closed.
    public float grip;
    public float gripSpeed = 3.0f;
    public GripState gripState = GripState.Fixed;
    public bool Close = false;

    [SerializeField] int stableFramesRequired = 3;           // ex) 2 또는 3
    [SerializeField] float gripStabilityEpsilon = 0.002f;     // "거의 비슷" 판단 임계값

    Queue<float> _gripSamples = new Queue<float>();
    int _lastSampledFrame = -1;
    GripState _lastGripState = GripState.Fixed;

    public bool IsClosed()
    {
        // 상태 전환 시(특히 Closing 시작 시) 샘플 초기화
        if (_lastGripState != gripState)
        {
            if (gripState == GripState.Closing)
            {
                _gripSamples.Clear();
                _lastSampledFrame = -1;
            }
            _lastGripState = gripState;
        }

        // 프레임당 한 번만 샘플링
        if (_lastSampledFrame != Time.frameCount)
        {
            _lastSampledFrame = Time.frameCount;
            float g = CurrentGrip();
            _gripSamples.Enqueue(g);
            while (_gripSamples.Count > stableFramesRequired)
                _gripSamples.Dequeue();
        }

        if (gripState != GripState.Closing) return false;
        if (_gripSamples.Count < stableFramesRequired) return false;

        float min = float.MaxValue, max = float.MinValue;
        foreach (var s in _gripSamples)
        {
            if (s < min) min = s;
            if (s > max) max = s;
        }

        return (max - min) <= gripStabilityEpsilon;
    }

    void Start()
    {
        fingerAController = fingerA.GetComponent<PincherFingerController>();
        fingerBController = fingerB.GetComponent<PincherFingerController>();
    }

    void FixedUpdate()
    {
        UpdateGrip();
        UpdateFingersForGrip();
        if (Close)
        {
            gripState = GripState.Closing;
            Close = false;
        }
    }


    // READ

    public float CurrentGrip()
    {
        // TODO - we can't really assume the fingers agree, need to think about that
        float meanGrip = (fingerAController.CurrentGrip() + fingerBController.CurrentGrip()) / 2.0f;
        return meanGrip;
    }


    public Vector3 CurrentGraspCenter()
    {
        /* Gets the point directly between the middle of the pincher fingers,
         * in the global coordinate system.      
         */
        Vector3 localCenterPoint = (fingerAController.GetOpenPosition() + fingerBController.GetOpenPosition()) / 2.0f;
        Vector3 globalCenterPoint = transform.TransformPoint(localCenterPoint);
        return globalCenterPoint;
    }


    // CONTROL

    public void ResetGripToOpen()
    {
        grip = 0.0f;
        fingerAController.ForceOpen(transform);
        fingerBController.ForceOpen(transform);
        gripState = GripState.Fixed;
    }

    // GRIP HELPERS

    void UpdateGrip()
    {
        if (gripState != GripState.Fixed)
        {
            float gripChange = (float)gripState * gripSpeed * Time.fixedDeltaTime;
            float gripGoal = CurrentGrip() + gripChange;
            grip = Mathf.Clamp01(gripGoal);
        }
    }

    void UpdateFingersForGrip()
    {
        fingerAController.UpdateGrip(grip);
        fingerBController.UpdateGrip(grip);
    }





}
