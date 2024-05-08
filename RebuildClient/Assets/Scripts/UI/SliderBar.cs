using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.UI
{
    public class SliderBar : MonoBehaviour
    {
        public SlicedFilledImage ProgressBar;
        public Image Background;

        public void SetProgress(float val)
        {
            ProgressBar.fillAmount = val;
        }

        public void SetColor(Color32 color)
        {
            ProgressBar.color = color;
        }
    }
}
