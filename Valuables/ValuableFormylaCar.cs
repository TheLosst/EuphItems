using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

public class ValuableFormylaCar : Trap
{
	public enum State
	{
		Inactive = 0,
		Idle = 1,
		MoveForward = 2
	}

	[Header("Animation")]
	public Transform carBody;

	public Transform wheelsFront;

	public Transform wheelsBack;

	public Transform driverBody;

	public Transform driverArms;

	public AnimationCurve carBodyCurve;

	private float carBodyLerp;

	private float wheelsLerp;

	private float wheelsSpin;

	private Quaternion wheelsFrontBaseRotation;

	private Quaternion wheelsBackBaseRotation;

	private float driverBodyLerp = 0.9f;

	public AnimationCurve driverArmsCurve;

	private float driverArmsLerp;

	[Header("Sounds")]
	public Sound sfxCarHorn;

	public Sound sfxCarStart;

	public Sound sfxCarStop;

	public Sound sfxCarLoop;

	private bool grabbedPrev;

	private bool loopPlaying;

	private bool loopPlayingPrevious;

	private float loopPitch;

	[Header("Colliders")]
	public List<Collider> carColliders;

	public PhysicMaterial defaultPhysicMaterial;

	public PhysicMaterial movingPhysicMaterial;

	public Transform centerTransform;

	private PhysGrabObjectImpactDetector impactDetector;

	private Vector3 boxSize = new Vector3(0.16f, 0.1f, 0.42f);

	private bool stuck;

	private float stuckTime;

	private Vector3 previousPosition;

	private State currentState;

	private bool stateImpulse;

	private float stateTimer;

	private float playerNearTimer;

	protected override void Start()
	{
		base.Start();
		impactDetector = GetComponent<PhysGrabObjectImpactDetector>();
		currentState = State.Inactive;
		if (wheelsFront != null)
		{
			wheelsFrontBaseRotation = wheelsFront.localRotation;
		}
		if (wheelsBack != null)
		{
			wheelsBackBaseRotation = wheelsBack.localRotation;
		}
	}

	protected override void Update()
	{
		base.Update();
		loopPlaying = currentState == State.MoveForward;
		loopPitch = Mathf.Lerp(loopPitch, 1f + physGrabObject.rb.velocity.magnitude * 1f, Time.deltaTime * 5f);
		sfxCarLoop.PlayLoop(loopPlaying, 0.8f, 0.8f, loopPitch);
		if (loopPlaying != loopPlayingPrevious)
		{
			if (loopPlaying)
			{
				sfxCarStart.Play(base.transform.position);
			}
			else
			{
				sfxCarStop.Play(base.transform.position);
			}
			loopPlayingPrevious = loopPlaying;
		}
		if (physGrabObject.grabbed)
		{
			if (!grabbedPrev)
			{
				grabbedPrev = true;
				if (SemiFunc.IsMasterClientOrSingleplayer())
				{
					PlayHonk();
					Quaternion turnX = Quaternion.Euler(-45f, 0f, 0f);
					Quaternion turnY = Quaternion.Euler(0f, 0f, 0f);
					Quaternion identity = Quaternion.identity;
					physGrabObject.TurnXYZ(turnX, turnY, identity);
				}
				if (!physGrabObject.grabbedLocal)
				{
				}
			}
		}
		else
		{
			grabbedPrev = false;
		}
		float num = 0.102f;
		if (currentState == State.MoveForward)
		{
			if (carBodyLerp < 1f)
			{
				carBodyLerp += Time.deltaTime * 4f;
			}
			if (carBodyLerp >= 1f)
			{
				carBodyLerp = 0f;
			}
			float num2 = carBodyCurve.Evaluate(carBodyLerp) * 0.015f;
			carBody.localPosition = new Vector3(carBody.localPosition.x, num2 + num, carBody.localPosition.z);
			if (wheelsLerp < 1f)
			{
				wheelsLerp += Time.deltaTime * 4f;
			}
			if (wheelsLerp >= 1f)
			{
				wheelsLerp = 0f;
			}
			float x = 720f;
			wheelsSpin += x * Time.deltaTime;
			if (wheelsFront != null)
			{
				wheelsFront.localRotation = wheelsFrontBaseRotation * Quaternion.Euler(wheelsSpin, 0f, 0f);
			}
			if (wheelsBack != null)
			{
				wheelsBack.localRotation = wheelsBackBaseRotation * Quaternion.Euler(wheelsSpin, 0f, 0f);
			}
			if (driverBodyLerp < 1f)
			{
				driverBodyLerp += Time.deltaTime * 1f;
			}
			if (driverBodyLerp >= 1f)
			{
				driverBodyLerp = 0f;
			}
			float z = carBodyCurve.Evaluate(carBodyLerp) * 6f;
			driverBody.localRotation = Quaternion.Euler(driverBody.localRotation.eulerAngles.x, driverBody.localRotation.eulerAngles.y, z);
			if (driverArmsLerp < 1f)
			{
				driverArmsLerp += Time.deltaTime * 1f;
			}
			if (driverArmsLerp >= 1f)
			{
				driverArmsLerp = 0f;
			}
			driverArms.localRotation = Quaternion.Euler(driverArms.localRotation.eulerAngles.x, driverArms.localRotation.eulerAngles.y, driverArmsCurve.Evaluate(driverArmsLerp) * 20f);
		}
		else if (stateImpulse)
		{
			carBody.localPosition = new Vector3(carBody.localPosition.x, num, carBody.localPosition.z);
			if (wheelsFront != null)
			{
				wheelsFront.localRotation = wheelsFrontBaseRotation;
			}
			if (wheelsBack != null)
			{
				wheelsBack.localRotation = wheelsBackBaseRotation;
			}
			driverArms.localRotation = Quaternion.Euler(driverArms.localRotation.eulerAngles.x, driverArms.localRotation.eulerAngles.y, driverArms.localRotation.eulerAngles.z);
		}
		StateMachine(_fixedUpdate: false);
		if (!SemiFunc.IsMasterClientOrSingleplayer())
		{
			return;
		}
		if (SemiFunc.PerSecond(5f, this))
		{
			if (currentState == State.Inactive)
			{
				return;
			}
			if (impactDetector.inCart || physGrabObject.playerGrabbing.Count > 0)
			{
				UpdateState(State.Idle);
				return;
			}
			bool flag = false;
			Collider[] array = Physics.OverlapBox(centerTransform.position, boxSize / 2f, base.transform.rotation, SemiFunc.LayerMaskGetVisionObstruct());
			for (int i = 0; i < array.Length; i++)
			{
				if (!array[i].GetComponentInParent<ValuableFormylaCar>())
				{
					flag = true;
					if (currentState != State.MoveForward && currentState != 0)
					{
						UpdateState(State.MoveForward);
					}
					break;
				}
			}
			if (!flag)
			{
				UpdateState(State.Idle);
			}
			else
			{
				StuckCheck();
			}
		}
		if (stuck)
		{
			stuckTime += Time.deltaTime;
			if (stuckTime >= 15f)
			{
				UpdateState(State.Inactive);
			}
		}
	}

	public void FixedUpdate()
	{
		StateMachine(_fixedUpdate: true);
	}

	private void StateInactive(bool _fixedUpdate)
	{
		if (!_fixedUpdate && physGrabObject.grabbed)
		{
			UpdateState(State.Idle);
		}
	}

	private void StateIdle(bool _fixedUpdate)
	{
		if (_fixedUpdate || !stateImpulse)
		{
			return;
		}
		foreach (Collider carCollider in carColliders)
		{
			if (!(carCollider == null))
			{
				carCollider.material = defaultPhysicMaterial;
			}
		}
		stateImpulse = false;
	}

	private void StateMoveForward(bool _fixedUpdate)
	{
		if (!_fixedUpdate)
		{
			return;
		}
		if (stateImpulse)
		{
			stateTimer = 2f;
			foreach (Collider carCollider in carColliders)
			{
				if (!(carCollider == null))
				{
					carCollider.material = movingPhysicMaterial;
				}
			}
			stateImpulse = false;
		}
		stateTimer -= Time.fixedDeltaTime;
		if (stateTimer <= 0f)
		{
			stateTimer = 1f;
			if (Random.Range(0f, 1f) > 0.5f)
			{
				TurnCar();
			}
		}
		float num = 4f - physGrabObject.rb.velocity.magnitude;
		physGrabObject.rb.AddForce(base.transform.forward * num * Time.fixedDeltaTime, ForceMode.Impulse);
		Vector3 torque = new Vector3(0f, Mathf.Sin(Time.time * 10f) * 0.006f, 0f);
		physGrabObject.rb.AddTorque(torque, ForceMode.Impulse);
		if (playerNearTimer >= 0f)
		{
			playerNearTimer -= Time.deltaTime;
			return;
		}
		playerNearTimer = 1f;
		bool flag = false;
		foreach (PlayerAvatar item in SemiFunc.PlayerGetList())
		{
			if (Vector3.Distance(item.transform.position, base.transform.position) < 25f)
			{
				flag = true;
				break;
			}
		}
		if (!flag)
		{
			UpdateState(State.Inactive);
		}
	}

	private void UpdateState(State _state)
	{
		if (SemiFunc.IsMasterClientOrSingleplayer() && _state != currentState)
		{
			stuck = false;
			stuckTime = 0f;
			currentState = _state;
			stateImpulse = true;
			if (SemiFunc.IsMultiplayer())
			{
				photonView.RPC("UpdateStateRPC", RpcTarget.All, currentState);
			}
			else
			{
				UpdateStateRPC(currentState);
			}
		}
	}

	[PunRPC]
	private void UpdateStateRPC(State _state, PhotonMessageInfo _info = default(PhotonMessageInfo))
	{
		if (SemiFunc.MasterOnlyRPC(_info))
		{
			currentState = _state;
		}
	}

	private void PlayHonk()
	{
		EnemyDirector.instance.SetInvestigate(base.transform.position, 15f);
		if (SemiFunc.IsMultiplayer())
		{
			photonView.RPC("PlayHonkRPC", RpcTarget.All);
		}
		else
		{
			PlayHonkRPC();
		}
	}

	[PunRPC]
	private void PlayHonkRPC(PhotonMessageInfo _info = default(PhotonMessageInfo))
	{
		if (SemiFunc.MasterOnlyRPC(_info))
		{
			sfxCarHorn.Play(base.transform.position);
		}
	}

	private void StateMachine(bool _fixedUpdate)
	{
		if (SemiFunc.IsMasterClientOrSingleplayer())
		{
			switch (currentState)
			{
			case State.Inactive:
				StateInactive(_fixedUpdate);
				break;
			case State.Idle:
				StateIdle(_fixedUpdate);
				break;
			case State.MoveForward:
				StateMoveForward(_fixedUpdate);
				break;
			}
		}
	}

	private void TurnCar()
	{
		Vector3 torque = new Vector3(0f, Random.Range(-1f, 1f) * 0.1f, 0f);
		physGrabObject.rb.AddTorque(torque, ForceMode.Impulse);
		PlayHonk();
	}

	private void StuckCheck()
	{
		if (currentState != 0 && currentState != State.Idle)
		{
			float magnitude = (wheelsFront.position - previousPosition).magnitude;
			previousPosition = wheelsFront.position;
			if (magnitude >= 0.2f)
			{
				stuck = false;
				stuckTime = 0f;
			}
			else
			{
				stuck = true;
			}
		}
	}
}
