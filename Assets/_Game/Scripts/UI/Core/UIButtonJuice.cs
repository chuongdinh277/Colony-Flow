using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DG.Tweening;

namespace ColonyFlow
{
    [RequireComponent(typeof(Button))]
    public class UIButtonJuice : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        private Vector3 originalScale;
        private Tween currentTween;
        private Button button;
        private bool isPointerDown = false;

        private void Awake()
        {
            originalScale = transform.localScale;
            button = GetComponent<Button>();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!button.interactable) return;
            isPointerDown = true;
            currentTween?.Kill();
            // Shrink slightly when pressed
            currentTween = transform.DOScale(originalScale * 0.9f, 0.1f).SetUpdate(true);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!isPointerDown) return;
            isPointerDown = false;
            
            if (!button.interactable)
            {
                currentTween?.Kill();
                transform.localScale = originalScale;
                return;
            }

            currentTween?.Kill();
            // Pop up then bounce back to normal
            currentTween = transform.DOScale(originalScale * 1.15f, 0.1f)
                .SetUpdate(true)
                .SetEase(Ease.OutQuad)
                .OnComplete(() => {
                    currentTween = transform.DOScale(originalScale, 0.15f)
                        .SetUpdate(true)
                        .SetEase(Ease.InOutSine);
                });
        }

        private void OnDisable()
        {
            currentTween?.Kill();
            transform.localScale = originalScale;
            isPointerDown = false;
        }
    }
}
