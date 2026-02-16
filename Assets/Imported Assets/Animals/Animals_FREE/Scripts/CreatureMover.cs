using System;
using UnityEngine;

namespace Controller
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(Animator))]
    [DisallowMultipleComponent]
    public class CreatureMover : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float m_WalkSpeed = 1f;
        [SerializeField] private float m_RunSpeed = 4f;
        [SerializeField, Range(0f, 360f)] private float m_RotateSpeed = 90f;
        [SerializeField] private Space m_Space = Space.Self;
        [SerializeField] private float m_JumpHeight = 5f;

        [Header("Animator")]
        [SerializeField] private string m_VerticalID = "Vert";
        [SerializeField] private string m_StateID = "State";

        private Transform m_Transform;
        private CharacterController m_Controller;
        private Animator m_Animator;

        private MovementHandler m_Movement;
        private AnimationHandler m_Animation;

        private Vector2 m_Axis;
        private Vector3 m_Target;
        private bool m_IsRun;
        private bool m_IsMoving;

        public Vector2 Axis => m_Axis;
        public Vector3 Target => m_Target;
        public bool IsRun => m_IsRun;

        private void Awake()
        {
            m_Transform = transform;
            m_Controller = GetComponent<CharacterController>();
            m_Animator = GetComponent<Animator>();

            m_Movement = new MovementHandler(m_Controller, m_Transform, m_WalkSpeed, m_RunSpeed, m_RotateSpeed, m_JumpHeight, m_Space);
            m_Animation = new AnimationHandler(m_Animator, m_VerticalID, m_StateID);
        }

        private void Update()
        {
            m_Movement.Move(Time.deltaTime, in m_Axis, in m_Target, m_IsRun, m_IsMoving, out var animAxis, out var isAir);
            m_Animation.Animate(in animAxis, m_IsRun ? 1f : 0f, Time.deltaTime);
        }

        public void SetInput(in Vector2 axis, in Vector3 target, in bool isRun, in bool isJump)
        {
            m_Axis = axis;
            m_Target = target;
            m_IsRun = isRun;
            m_IsMoving = axis.sqrMagnitude > Mathf.Epsilon;
        }

        #region Handlers
        private class MovementHandler
        {
            private readonly CharacterController m_Controller;
            private readonly Transform m_Transform;

            private float m_WalkSpeed;
            private float m_RunSpeed;
            private float m_RotateSpeed;
            private Space m_Space;

            private Vector3 m_GravityAcelleration = Physics.gravity;

            public MovementHandler(CharacterController controller, Transform transform, float walkSpeed, float runSpeed, float rotateSpeed, float jumpHeight, Space space)
            {
                m_Controller = controller;
                m_Transform = transform;
                m_WalkSpeed = walkSpeed;
                m_RunSpeed = runSpeed;
                m_RotateSpeed = rotateSpeed;
                m_Space = space;
            }

            public void Move(float deltaTime, in Vector2 axis, in Vector3 target, bool isRun, bool isMoving, out Vector2 animAxis, out bool isAir)
            {
                Vector3 forward = Vector3.Normalize(target - m_Transform.position);
                Vector3 movement = axis.y * forward + axis.x * Vector3.Cross(Vector3.up, forward);

                ApplyGravity(deltaTime, out isAir);
                Vector3 displacement = (isRun ? m_RunSpeed : m_WalkSpeed) * movement + m_GravityAcelleration;
                m_Controller.Move(displacement * deltaTime);

                Turn(forward, isMoving);

                animAxis = new Vector2(Vector3.Dot(movement, m_Transform.right), Vector3.Dot(movement, m_Transform.forward));
            }

            private void ApplyGravity(float deltaTime, out bool isAir)
            {
                if (m_Controller.isGrounded)
                {
                    m_GravityAcelleration = Physics.gravity;
                    isAir = false;
                    return;
                }

                isAir = true;
                m_GravityAcelleration += Physics.gravity * deltaTime;
            }

            private void Turn(Vector3 forward, bool isMoving)
            {
                float angle = Vector3.SignedAngle(m_Transform.forward, forward, Vector3.up);
                if (Mathf.Abs(angle) < 0.1f) return;
                m_Transform.Rotate(Vector3.up, Mathf.Sign(angle) * Mathf.Min(Mathf.Abs(angle), m_RotateSpeed * Time.deltaTime));
            }
        }

        private class AnimationHandler
        {
            private readonly Animator m_Animator;
            private readonly string m_VerticalID;
            private readonly string m_StateID;

            public AnimationHandler(Animator animator, string verticalID, string stateID)
            {
                m_Animator = animator;
                m_VerticalID = verticalID;
                m_StateID = stateID;
            }

            public void Animate(in Vector2 axis, float state, float deltaTime)
            {
                m_Animator.SetFloat(m_VerticalID, axis.magnitude);
                m_Animator.SetFloat(m_StateID, state);
            }
        }
        #endregion
    }
}
