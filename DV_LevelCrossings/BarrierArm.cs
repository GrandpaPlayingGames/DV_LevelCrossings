using UnityEngine;
using DV_LevelCrossings;

public class BarrierArm
{
    private readonly Transform root;
    public Transform RootTransform => root;
    private readonly Transform ramp;
    private readonly Collider blockingCollider;

    private float currentAngle;
    private float targetAngle;

    private const float UpAngle = 90f;
    private const float DownAngle = 0f;
	//private const float RotateSpeedDegPerSec = 45f;
	//private float rotateSpeedDegPerSec = 45f;

	public BarrierArm(Transform rootTransform)
    {
        root = rootTransform;

        ramp = root != null ? root.Find("Ramp") : null;
        if (ramp == null)
        {
            Main.Log("[Crossing] Ramp not found on " + (root != null ? root.name : "<null>"));
            return;
        }

        var colT = ramp.Find("Colliders/Collider");
        blockingCollider = colT != null ? colT.GetComponent<Collider>() : null;

        currentAngle = UpAngle;
        targetAngle = UpAngle;

        ApplyRotationImmediate();
        SetBlockingCollider(false);
    }

    public void SetDown()
    {
        targetAngle = DownAngle;
        SetBlockingCollider(true);
 
    }

    public void SetUp()
    {
        targetAngle = UpAngle;
        SetBlockingCollider(false);
    }

	public void Tick()
	{
		if (ramp == null) return;

		float speed = Main.Settings.useSlowBarrierSpeed
			? Main.Settings.slowBarrierSpeed
			: Main.Settings.normalBarrierSpeed;

		currentAngle = Mathf.MoveTowards(
			currentAngle,
			targetAngle,
			speed * Time.deltaTime
		);

		ramp.localEulerAngles = new Vector3(0f, 0f, currentAngle);
	}

	private void ApplyRotationImmediate()
    {
        if (ramp == null) return;
        ramp.localEulerAngles = new Vector3(0f, 0f, currentAngle);
    }

    private void SetBlockingCollider(bool enabled)
    {
        if (blockingCollider != null)
            blockingCollider.enabled = enabled;
    }
}

public class BarrierMember : MonoBehaviour
{
    public CrossingController controller;
    public Transform barrierRoot;
}