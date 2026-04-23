using System.Collections;
using UnityEngine;

namespace HuanYouYu.MiniGameHall
{
    [DisallowMultipleComponent]
    public sealed class UiTweenRunner : MonoBehaviour
    {
        public Coroutine Run(IEnumerator routine)
        {
            return StartCoroutine(routine);
        }
    }
}
