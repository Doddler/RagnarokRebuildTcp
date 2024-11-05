using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.UI
{
    public class SliderBar : MonoBehaviour
    {
        public SlicedFilledImage ProgressBar;
        public Image Background;
        public SlicedFilledImage ProgressDelayed;
        private bool isMoving;
        private float accel;

        public void SetProgress(float val, bool noAnim = true)
        {
            // Debug.Log($"SetProgress {val} {noAnim}");
            ProgressBar.fillAmount = val;
            if (!ProgressDelayed)
                return;
            
            if (noAnim || val > ProgressDelayed.fillAmount)
            {
                ProgressDelayed.fillAmount = ProgressBar.fillAmount;
                ProgressDelayed.gameObject.SetActive(false);
                isMoving = false;
                return;
            }

            if (isMoving)
                return;
            
            ProgressDelayed.gameObject.SetActive(true);
            accel = 0;
            isMoving = true;
        }

        public void SetColor(Color32 color)
        {
            ProgressBar.color = color;
        }

        public void Update()
        {
            if (!isMoving)
                return;
            ProgressDelayed.fillAmount -= accel * Time.deltaTime;
            accel += Time.deltaTime * 20;
            if (ProgressDelayed.fillAmount < ProgressBar.fillAmount)
            {
                ProgressDelayed.gameObject.SetActive(false);
                isMoving = false;
            }
        }
    }
}
