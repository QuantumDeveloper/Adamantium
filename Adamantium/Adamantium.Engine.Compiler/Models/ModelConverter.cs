using System;
using System.Threading;
using System.Threading.Tasks;
using Adamantium.Engine.Compiler.Converter.Configs;
using Adamantium.Core;
using Adamantium.Engine.Core.Models;

namespace Adamantium.Engine.Compiler.Converter
{
    public class ModelConverter : PropertyChangedBase
    {
        #region Variables

        private CancellationTokenSource cancellationToken;

        private Int32 currentParsingProgress;
        private Int32 maximumParsingProgress;
        private Boolean convertationInProgress;
        private Boolean convertationFinished = true;
        private Boolean indeterminateLoadingState;
        private int convertFilesCount;

        #endregion


        #region Properties

        public Boolean ConvertationInProgress
        {
            get { return convertationInProgress; }
            set
            {
                convertationInProgress = value;
                RaisePropertyChanged();
            }
        }

        public Boolean ConvertationFinished
        {
            get { return convertationFinished; }
            set
            {
                convertationFinished = value;
                RaisePropertyChanged();
            }
        }

        public Int32 CurrentParsingProgressValue
        {
            get { return currentParsingProgress; }
            set
            {
                currentParsingProgress = value;
                RaisePropertyChanged();
            }
        }

        public Int32 MaximumParsingProgressValue
        {
            get { return maximumParsingProgress; }
            set
            {
                maximumParsingProgress = value;
                RaisePropertyChanged();
            }
        }

        public Boolean IndeterminateLoadingState
        {
            get { return indeterminateLoadingState; }
            set
            {
                indeterminateLoadingState = value;
                RaisePropertyChanged();
            }
        }

        #endregion


        public ModelConverter()
        {
            cancellationToken = new CancellationTokenSource();
        }


        public SceneData ImportFileAsync(string path)
        {
            return ModelConverterFactory.GetConverter(path, new ConversionConfig(true)).StartConvertion();
        }

        #region Методы по обработке файлов

        public Boolean IsConvertationCancelled { get; private set; }

        public void CancelConvertation()
        {
            cancellationToken.Cancel();
            ConvertationInProgress = false;
            ConvertationFinished = !ConvertationInProgress;
            IsConvertationCancelled = true;
        }

        #endregion

    }
}
