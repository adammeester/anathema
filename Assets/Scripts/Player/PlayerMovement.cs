using Anathema.Abilities;
using Anathema.Combat;
using UnityEngine;

namespace Anathema.Player
{
    // WASD moves relative to the (fixed) camera, independent of facing, so the character strafes while aiming.
    // Movement is instant: full speed immediately, stops immediately.
    [RequireComponent(typeof(CharacterController), typeof(Health))]
    public class PlayerMovement : MonoBehaviour
    {
        [SerializeField] float moveSpeed = 6.5f;
        [SerializeField] float gravity = -25f;

        CharacterController controller;
        Health health;
        Camera cam;
        Counter counter;
        Ability[] abilities;
        float verticalVelocity;

        public float MoveSpeed => moveSpeed;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            health = GetComponent<Health>();

            // Tuning values (health, speed, ability numbers) come from Resources/Stats/<Character>.json.
            CharacterStats.EnsureOn(gameObject);

            // Scenes built before the walk cycle existed don't have it on the model; add it so they still animate.
            Transform model = transform.Find("Model");
            if (model != null && model.GetComponent<ProceduralWalk>() == null) model.gameObject.AddComponent<ProceduralWalk>();
        }

        bool IsRootedByAbility()
        {
            if (abilities == null) abilities = GetComponents<Ability>();
            foreach (Ability ability in abilities)
                if (ability.RootsMovement) return true;
            return false;
        }

        void Update()
        {
            if (health.IsDead || health.IsReplica || Anathema.Net.NetGame.Frozen) return;
            if (cam == null) cam = Camera.main;

            // Can't move while countering (gravity still applies).
            if (counter == null) counter = GetComponent<Counter>();
            bool guarding = (counter != null && counter.IsGuarding) || Stunned.IsStunned(gameObject) || Rooted.IsRooted(gameObject) || IsRootedByAbility();

            Vector2 input = guarding ? Vector2.zero : new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            input = Vector2.ClampMagnitude(input, 1f);

            Vector3 forward = Vector3.forward;
            Vector3 right = Vector3.right;
            if (cam != null)
            {
                forward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
                right = Vector3.ProjectOnPlane(cam.transform.right, Vector3.up).normalized;
            }

            verticalVelocity = controller.isGrounded ? -1f : verticalVelocity + gravity * Time.deltaTime;

            var maimed = GetComponent<Maimed>();
            float speed = moveSpeed * (maimed != null ? maimed.SpeedMultiplier : 1f) * Enraged.MoveSpeedMultiplier(gameObject)
                * ReflectDome.SlowMultiplier(gameObject);
            Vector3 velocity = (forward * input.y + right * input.x) * speed;
            // Panic overrides steering: run straight away from whatever caused it (unless stunned/rooted/guarding).
            if (!guarding && Panicked.TryGetFleeDirection(gameObject, out Vector3 flee)) velocity = flee * speed;
            velocity.y = verticalVelocity;
            controller.Move(velocity * Time.deltaTime);
        }
    }
}
