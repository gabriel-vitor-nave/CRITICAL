using System;
using System.Collections;
using TMPro;
using UnityEngine;

namespace Critical.Procedural
{
    public sealed class DiceRoller : MonoBehaviour
    {
        public Animator animator;
        public TMP_Text resultText;
        [Min(0.1f)] public float rollDuration = 5f;
        [Min(0f)] public float resultHold = 0.8f;

        public int LastRoll { get; private set; }
        public event Action<int> RollCompleted;

        public IEnumerator Roll(System.Random random)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            if (animator == null || resultText == null)
                throw new InvalidOperationException("A animação e o texto do D20 precisam estar configurados.");

            LastRoll = random.Next(1, 21);
            resultText.text = "";
            animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            animator.enabled = true;
            animator.Play(0, 0, 0f);
            animator.Update(0f);
            yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, rollDuration));
            animator.enabled = false;
            resultText.text = LastRoll.ToString();
            RollCompleted?.Invoke(LastRoll);
        }

        // A coleta e a sala especial serão implementadas em outra etapa.
        public static bool CanTriggerCritical(int naturalRoll, int emptyFlasks) =>
            naturalRoll == 20 && emptyFlasks >= 2;
    }
}
