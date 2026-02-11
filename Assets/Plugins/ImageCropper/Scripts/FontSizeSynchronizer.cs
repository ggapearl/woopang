using UnityEngine;
using UnityEngine.UI;

namespace ImageCropperNamespace
{
    public class FontSizeSynchronizer : MonoBehaviour
    {
#pragma warning disable 0649
        [SerializeField]
        private Text[] texts;
#pragma warning restore 0649

        private int[] initialBestFitSizes;
        private Canvas canvas;

        private void Awake()
        {
            if( texts == null || texts.Length == 0 )
                return;

            if (texts[0] != null)
                canvas = texts[0].canvas;

            initialBestFitSizes = new int[texts.Length];
            for( int i = 0; i < texts.Length; i++ )
            {
                if (texts[i] != null)
                    initialBestFitSizes[i] = texts[i].resizeTextMaxSize;
            }
        }

        public void Synchronize()
        {
            if( canvas == null || !gameObject.activeInHierarchy )
                return;

            int minSize = int.MaxValue;
            for( int i = 0; i < texts.Length; i++ )
            {
                Text text = texts[i];
                if (text == null) continue;

                text.resizeTextMaxSize = initialBestFitSizes[i];
                text.resizeTextForBestFit = true;
                if (text.cachedTextGenerator != null)
                {
                    text.cachedTextGenerator.Populate( text.text, text.GetGenerationSettings( text.rectTransform.rect.size ) );
                    int fontSize = text.cachedTextGenerator.fontSizeUsedForBestFit;
                    if( fontSize < minSize )
                        minSize = fontSize;
                }
            }

            if (minSize == int.MaxValue) return;

            int fontSizeScaled = (int) ( minSize / canvas.scaleFactor );
            for( int i = 0; i < texts.Length; i++ )
            {
                if (texts[i] != null)
                {
                    texts[i].fontSize = fontSizeScaled;
                    texts[i].resizeTextForBestFit = false;
                }
            }
        }
    }
}