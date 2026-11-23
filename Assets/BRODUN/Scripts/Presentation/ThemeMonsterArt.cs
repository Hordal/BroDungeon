using System;
using System.Linq;
using UnityEngine;

namespace Brodun.Presentation
{
    public sealed class ThemeMonsterArt : ScriptableObject
    {
        [SerializeField] private GameObject[] prefabs;

        public GameObject Find(string id) => Array.Find(prefabs, prefab => prefab != null && prefab.name == id);
    }

    // Sample the original prefab clips, then draw their pose on the battle's existing UI Image.
    // The prefab Animator never plays, so demo effects and animation events cannot drive combat.
    internal sealed class ImportedMonsterPose
    {
        private readonly GameObject source;
        private readonly SpriteRenderer visual;
        private readonly AnimationClip[] clips;
        private readonly Vector3 baseScale;
        private readonly float bodyHeight;
        public Sprite Sprite => visual.sprite;
        public AnimationClip Idle { get; }
        public AnimationClip Attack { get; }
        public AnimationClip Hit { get; }
        public AnimationClip Block { get; }
        public AnimationClip Death { get; }
        public float AttackContact { get; }

        public ImportedMonsterPose(GameObject prefab, Transform parent)
        {
            source = UnityEngine.Object.Instantiate(prefab, parent, false);
            var animator = source.GetComponent<Animator>();
            animator.enabled = false;
            clips = animator.runtimeAnimatorController.animationClips;
            visual = source.GetComponentInChildren<SpriteRenderer>();
            visual.forceRenderingOff = true;
            baseScale = visual.transform.localScale;
            bodyHeight = visual.sprite.bounds.size.y * baseScale.y;
            Idle = Clip("Idle");
            Attack = Clip("Attack");
            Hit = Clip("Hit");
            Block = Clip("Block");
            Death = Clip("Death");
            AttackContact = Attack.events.First(e => e.functionName == "OnAttackImpact").time / Attack.length;
        }

        private AnimationClip Clip(string motion) =>
            Array.Find(clips, clip => clip.name.EndsWith("_" + motion, StringComparison.Ordinal));

        public void Apply(AnimationClip clip, float progress, UnityEngine.UI.Image sprite, Vector2 home, float height, Color tint)
        {
            clip.SampleAnimation(source, Mathf.Clamp01(progress) * clip.length);
            var pose = visual.transform;
            var rect = sprite.rectTransform;
            float pixelsPerUnit = height / bodyHeight;
            rect.anchoredPosition = home + new Vector2(
                Mathf.Clamp(pose.localPosition.x * pixelsPerUnit, -44f, 44f),
                pose.localPosition.y * pixelsPerUnit);
            rect.localRotation = pose.localRotation;
            rect.localScale = new Vector3(pose.localScale.x / baseScale.x, pose.localScale.y / baseScale.y, 1);
            sprite.color = visual.color * tint;
        }
    }
}
