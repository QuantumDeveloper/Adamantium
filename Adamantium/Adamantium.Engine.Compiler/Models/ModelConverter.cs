using System;
using System.Threading;
using Adamantium.Core;
using Adamantium.Engine.Compiler.Converter;
using Adamantium.Engine.Compiler.Converter.Configs;
using Adamantium.Engine.Core.Models;

namespace Adamantium.Engine.Compiler.Models
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
            get => convertationInProgress;
            set
            {
                convertationInProgress = value;
                RaisePropertyChanged();
            }
        }

        public Boolean ConvertationFinished
        {
            get => convertationFinished;
            set
            {
                convertationFinished = value;
                RaisePropertyChanged();
            }
        }

        public Int32 CurrentParsingProgressValue
        {
            get => currentParsingProgress;
            set
            {
                currentParsingProgress = value;
                RaisePropertyChanged();
            }
        }

        public Int32 MaximumParsingProgressValue
        {
            get => maximumParsingProgress;
            set
            {
                maximumParsingProgress = value;
                RaisePropertyChanged();
            }
        }

        public Boolean IndeterminateLoadingState
        {
            get => indeterminateLoadingState;
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
            return ModelConverterFactory.GetConverter(path, new ConversionConfig(true)).StartConversion();
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
