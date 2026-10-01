using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SpellSlinger
{
    // An NPC you can talk to. Mentors teach you their sects spell when you finish talking to them
    public class NPC : MonoBehaviour
    {
        public static readonly List<NPC> All = new();

        public string displayName;
        public string title;
        public Color color = Color.white;
        [TextArea(2, 4)] public string[] lines;
        [TextArea(2, 4)] public string[] linesAfterLearning;
        [Tooltip("If this is on they teach their sects spell at the end of the conversation")]
        public bool teaches;
        public Element teachesElement;

        public bool IsMentor => teaches;
        public Vector3 HeadPosition => transform.position + Vector3.up * 2.2f;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        void Update()
        {
            // Turn to look at the player when they get close
            var player = PlayerController.Instance;
            if (!player) return;
            Vector3 to = player.transform.position - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 100f && to.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), 1f - Mathf.Exp(-5f * Time.deltaTime));
        }

        public string[] CurrentLines =>
            teaches && PlayerMagic.Knows(teachesElement) && linesAfterLearning != null && linesAfterLearning.Length > 0 ? linesAfterLearning : lines;
    }
}
