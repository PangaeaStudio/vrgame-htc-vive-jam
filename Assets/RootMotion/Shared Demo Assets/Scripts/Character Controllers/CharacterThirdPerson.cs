﻿using UnityEngine;
using System.Collections;
using RootMotion.FinalIK;

namespace RootMotion.Demos {

	/// <summary>
	/// Third person character controller. This class is based on the ThirdPersonCharacter.cs of the Unity Exmaple Assets.
	/// </summary>
	public class CharacterThirdPerson : CharacterBase {

		// Is the character always rotating to face the move direction or is he strafing?
		[System.Serializable]
		public enum MoveMode {
			Directional,
			Strafe
		}

		// Animation state
		public struct AnimState {
			public Vector3 moveDirection; // the forward speed
			public bool jump; // should the character be jumping?
			public bool crouch; // should the character be crouching?
			public bool onGround; // is the character grounded
			public bool isStrafing; // should the character always rotate to face the move direction or strafe?
			public float yVelocity; // y velocity of the character
		}

		[Header("References")]
		[SerializeField] CharacterAnimationBase characterAnimation; // the animation controller
		[SerializeField] UserControlThirdPerson userControl; // user input
		[SerializeField] CameraController cam; // Camera controller (optional). If assigned will update the camera in LateUpdate only if character moves
		[SerializeField] protected Grounder grounder; // reference to the Grounder

		[Header("Movement")]
		public MoveMode moveMode; // Is the character always rotating to face the move direction or is he strafing?
		public bool smoothPhysics = true; // If true, will use interpolation to smooth out the fixed time step.
		public float smoothAccelerationTime = 0.2f; // The smooth acceleration of the speed of the character (using Vector3.SmoothDamp)
		public float linearAccelerationSpeed = 3f; // The linear acceleration of the speed of the character (using Vector3.MoveTowards)
		public float platformFriction = 7f;					// the acceleration of adapting the velocities of moving platforms
		public float groundStickyEffect = 4f;				// power of 'stick to ground' effect - prevents bumping down slopes.
		public float maxVerticalVelocityOnGround = 3f;		// the maximum y velocity while the character is grounded
		public float velocityToGroundTangentWeight = 0f;	// the weight of rotating character velocity vector to the ground tangent

		[Header("Rotation")]
		public bool lookInCameraDirection; // should the character be looking in the same direction that the camera is facing
		public float turnSpeed = 5f;					// additional turn speed added when the player is moving (added to animation root rotation)
		public float stationaryTurnSpeedMlp = 1f;			// additional turn speed added when the player is stationary (added to animation root rotation)

		[Header("Jumping and Falling")]
		public float airSpeed = 6f; // determines the max speed of the character while airborne
		public float airControl = 2f; // determines the response speed of controlling the character while airborne
		public float jumpPower = 12f; // determines the jump force applied when jumping (and therefore the jump height)
		public float jumpRepeatDelayTime = 0f;			// amount of time that must elapse between landing and being able to jump again

		[Header("Wall Running")]

		[SerializeField] LayerMask wallRunLayers; // walkable vertical surfaces
		public float wallRunMaxLength = 1f;					// max duration of a wallrun
		public float wallRunMinMoveMag = 0.6f;				// the minumum magnitude of the user control input move vector
		public float wallRunMinVelocityY = -1f;				// the minimum vertical velocity of doing a wall run
		public float wallRunRotationSpeed = 1.5f;			// the speed of rotating the character to the wall normal
		public float wallRunMaxRotationAngle = 70f;			// max angle of character rotation
		public float wallRunWeightSpeed = 5f;				// the speed of blending in/out the wall running effect

		[Header("Crouching")]
		public float crouchCapsuleScaleMlp = 0.6f;			// the capsule collider scale multiplier while crouching

		public bool onGround { get; private set; }
		public AnimState animState = new AnimState();

		private Vector3 moveDirection; // The current move direction of the character in Strafe move mode
		private Animator animator;
		private Vector3 normal, platformVelocity;
		private RaycastHit hit;
		private float jumpLeg, jumpEndTime, forwardMlp, groundDistance, lastAirTime, stickyForce;
		private Vector3 wallNormal = Vector3.up;
		private Vector3 moveDirectionVelocity;
		private float wallRunWeight;
		private float lastWallRunWeight;
		private Vector3 fixedDeltaPosition;
		private bool fixedFrame;
		private float wallRunEndTime;

		// Use this for initialization
		protected override void Start () {
			base.Start();

			animator = GetComponent<Animator>();

			wallNormal = Vector3.up;
			onGround = true;
			animState.onGround = true;

			if (cam != null) cam.enabled = false;
		}

		void OnAnimatorMove() {
			Move (animator.deltaPosition);
		}

		// When the Animator moves
		public override void Move(Vector3 deltaPosition) {
			// Accumulate delta position, update in FixedUpdate to maintain consitency
			fixedDeltaPosition += deltaPosition;
		}

		void FixedUpdate() {
			// Smoothing out the fixed time step
			r.interpolation = smoothPhysics? RigidbodyInterpolation.Interpolate: RigidbodyInterpolation.None;
			characterAnimation.smoothFollow = smoothPhysics;

			// Move
			MoveFixed(fixedDeltaPosition);
			fixedDeltaPosition = Vector3.zero;

			Rotate();

			GroundCheck (); // detect and stick to ground

			// Friction
			if (userControl.state.move == Vector3.zero && groundDistance < airborneThreshold * 0.5f) HighFriction();
			else ZeroFriction();
			
			if (onGround) {
				// Jumping
				animState.jump = Jump();
			} else {
				
				// Additional gravity
				r.AddForce((Physics.gravity * gravityMultiplier) - Physics.gravity);
			}
			
			// Scale the capsule colllider while crouching
			ScaleCapsule(userControl.state.crouch? crouchCapsuleScaleMlp: 1f);
			

			fixedFrame = true;

		}

		void Update() {
			// Fill in animState
			animState.onGround = onGround;
			animState.moveDirection = GetMoveDirection();
			animState.yVelocity = r.velocity.y;
			animState.crouch = userControl.state.crouch;
			animState.isStrafing = moveMode == MoveMode.Strafe;
		}

		void LateUpdate() {
			if (cam == null) return;
			
			cam.UpdateInput();
			
			if (!fixedFrame && r.interpolation == RigidbodyInterpolation.None) return;
			
			// Update camera only if character moves
			cam.UpdateTransform(r.interpolation == RigidbodyInterpolation.None? Time.fixedDeltaTime: Time.deltaTime);
			
			fixedFrame = false;
		}

		private void MoveFixed(Vector3 deltaPosition) {
			// Process horizontal wall-running
			WallRun();
			
			Vector3 velocity = deltaPosition / Time.deltaTime;
			
			// Add velocity of the rigidbody the character is standing on
			velocity += new Vector3(platformVelocity.x, 0f, platformVelocity.z);
			
			if (onGround) {
				// Rotate velocity to ground tangent
				if (velocityToGroundTangentWeight > 0f) {
					Quaternion rotation = Quaternion.FromToRotation(transform.up, normal);
					velocity = Quaternion.Lerp(Quaternion.identity, rotation, velocityToGroundTangentWeight) * velocity;
				}
			} else {
				// Air move
				Vector3 airMove = new Vector3 (userControl.state.move.x * airSpeed, 0f, userControl.state.move.z * airSpeed);
				velocity = Vector3.Lerp(r.velocity, airMove, Time.deltaTime * airControl);
			}
			
			if (onGround && Time.time > jumpEndTime) {
				r.velocity = r.velocity - transform.up * stickyForce * Time.deltaTime;
			}
			
			// Vertical velocity
			velocity.y = Mathf.Clamp(r.velocity.y, r.velocity.y, onGround? maxVerticalVelocityOnGround: r.velocity.y);
			
			r.velocity = velocity;
			
			// Dampering forward speed on the slopes
			float slopeDamper = !onGround? 1f: GetSlopeDamper(-deltaPosition / Time.deltaTime, normal);
			forwardMlp = Mathf.Lerp(forwardMlp, slopeDamper, Time.deltaTime * 5f);
		}

		// Processing horizontal wall running
		private void WallRun() {
			bool canWallRun = CanWallRun();

			// Remove flickering in and out of wall-running
			if (wallRunWeight > 0f && !canWallRun) wallRunEndTime = Time.time;
			if (Time.time < wallRunEndTime + 0.5f) canWallRun = false;

			wallRunWeight = Mathf.MoveTowards(wallRunWeight, (canWallRun? 1f: 0f), Time.deltaTime * wallRunWeightSpeed);
			
			if (wallRunWeight <= 0f) {
				// Reset
				if (lastWallRunWeight > 0f) {
					transform.rotation = Quaternion.LookRotation(new Vector3(transform.forward.x, 0f, transform.forward.z), Vector3.up);
					wallNormal = Vector3.up;
				}
			}
			lastWallRunWeight = wallRunWeight;
			
			if (wallRunWeight <= 0f) return;
			
			// Make sure the character won't fall down
			if (onGround && r.velocity.y < 0f) r.velocity = new Vector3(r.velocity.x, 0f, r.velocity.z);
			
			// transform.forward flattened
			Vector3 f = transform.forward;
			f.y = 0f;
			
			// Raycasting to find a walkable wall
			RaycastHit velocityHit = new RaycastHit();
			velocityHit.normal = Vector3.up;
			Physics.Raycast(onGround? transform.position: capsule.bounds.center, f, out velocityHit, 3f, wallRunLayers);
			
			// Finding the normal to rotate to
			wallNormal = Vector3.Lerp(wallNormal, velocityHit.normal, Time.deltaTime * wallRunRotationSpeed);
			
			// Clamping wall normal to max rotation angle
			wallNormal = Vector3.RotateTowards(Vector3.up, wallNormal, wallRunMaxRotationAngle * Mathf.Deg2Rad, 0f);
			
			// Get transform.forward ortho-normalized to the wall normal
			Vector3 fW = transform.forward;
			Vector3 nW = wallNormal;
			Vector3.OrthoNormalize(ref nW, ref fW);
			
			// Rotate from upright to wall normal
			transform.rotation = Quaternion.Slerp(Quaternion.LookRotation(f, Vector3.up), Quaternion.LookRotation(fW, wallNormal), wallRunWeight);
		}

		// Should the character be enabled to do a wall run?
		private bool CanWallRun() {
			if (Time.time < jumpEndTime - 0.1f) return false;
			if (Time.time > jumpEndTime - 0.1f + wallRunMaxLength) return false;
			if (r.velocity.y < wallRunMinVelocityY) return false;
			if (userControl.state.move.magnitude < wallRunMinMoveMag) return false;
			
			return true;
		}

		// Get the move direction of the character relative to the character rotation
		private Vector3 GetMoveDirection() {
			switch(moveMode) {
			case MoveMode.Directional:
				moveDirection = Vector3.SmoothDamp(moveDirection, new Vector3(0f, 0f, userControl.state.move.magnitude), ref moveDirectionVelocity, smoothAccelerationTime);
				moveDirection = Vector3.MoveTowards(moveDirection, new Vector3(0f, 0f, userControl.state.move.magnitude), Time.deltaTime * linearAccelerationSpeed);
				return moveDirection * forwardMlp;
			case MoveMode.Strafe:
				moveDirection = Vector3.SmoothDamp(moveDirection, userControl.state.move, ref moveDirectionVelocity, smoothAccelerationTime);
				moveDirection = Vector3.MoveTowards(moveDirection, userControl.state.move, Time.deltaTime * linearAccelerationSpeed);
				return transform.InverseTransformDirection(moveDirection);
			}

			return Vector3.zero;
		}

		// Rotate the character
		private void Rotate() {
			float angle = GetAngleFromForward(GetForwardDirection());
			
			if (userControl.state.move == Vector3.zero) angle *= (1.01f - (Mathf.Abs(angle) / 180f)) * stationaryTurnSpeedMlp;

			// Rotating the character
			RigidbodyRotateAround(characterAnimation.GetPivotPoint(), transform.up, angle * Time.deltaTime * turnSpeed);
		}

		// Which way to look at?
		private Vector3 GetForwardDirection() {
			bool isMoving = userControl.state.move != Vector3.zero;

			switch (moveMode) {
			case MoveMode.Directional:
				if (isMoving) return userControl.state.move;
				return lookInCameraDirection? userControl.state.lookPos - r.position: transform.forward;
			case MoveMode.Strafe:
				if (isMoving) return userControl.state.lookPos - r.position;
				return lookInCameraDirection? userControl.state.lookPos - r.position: transform.forward;
			}

			return Vector3.zero;
		}

		private bool Jump() {
			// check whether conditions are right to allow a jump:
			if (!userControl.state.jump) return false;
			if (userControl.state.crouch) return false;
			if (!characterAnimation.animationGrounded) return false;
			if (Time.time < lastAirTime + jumpRepeatDelayTime) return false;

			// Jump
			onGround = false;
			jumpEndTime = Time.time + 0.1f;

			Vector3 jumpVelocity = userControl.state.move * airSpeed;
			r.velocity = jumpVelocity;
			r.velocity += transform.up * jumpPower;

			return true;
		}

		// Spherecasting for the root
		private RaycastHit GetHit() {
			if (grounder == null) return GetSpherecastHit();
			if (grounder.solver.quality != Grounding.Quality.Best) return GetSpherecastHit(); // We need a spherecast so if grounding quality is lower and it returns a raycast, we need to do it ourselves.

			if (grounder.enabled && grounder.weight > 0f) return grounder.solver.rootHit; // Already calculated by Grounding
			return grounder.solver.GetRootHit(); // Grounding is not updated, so get a fresh root hit
		}

		// Is the character grounded?
		private void GroundCheck () {
			Vector3 platformVelocityTarget = Vector3.zero;
			float stickyForceTarget = 0f;

			// Spherecasting
			hit = GetHit();

			normal = hit.normal;
			groundDistance = r.position.y - hit.point.y;

			// if not jumping...
			bool findGround = Time.time > jumpEndTime && r.velocity.y < jumpPower * 0.5f;

			if (findGround) {
				bool g = onGround;
				onGround = false;

				// The distance of considering the character grounded
				float groundHeight = !g? airborneThreshold * 0.5f: airborneThreshold;

				Vector3 horizontalVelocity = r.velocity;
				horizontalVelocity.y = 0f;
				
				float velocityF = horizontalVelocity.magnitude;

				if (groundDistance < groundHeight) {
					// Force the character on the ground
					stickyForceTarget = groundStickyEffect * velocityF * groundHeight;

					// On moving platforms
					if (hit.rigidbody != null) platformVelocityTarget = hit.rigidbody.GetPointVelocity(hit.point);

					// Flag the character grounded
					onGround = true;
				}
			}

			// Interpolate the additive velocity of the platform the character might be standing on
			platformVelocity = Vector3.Lerp(platformVelocity, platformVelocityTarget, Time.deltaTime * platformFriction);

			stickyForce = stickyForceTarget;//Mathf.Lerp(stickyForce, stickyForceTarget, Time.deltaTime * 5f);

			// remember when we were last in air, for jump delay
			if (!onGround) lastAirTime = Time.time;
		}
	}
}
