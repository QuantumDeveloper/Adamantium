using System;
using System.Collections.Generic;
using System.Threading;
using Adamantium.Core;
using Adamantium.Engine.Compiler.Converter;
using Adamantium.Engine.Compiler.Converter.Configs;
using Adamantium.Graphics.Core.Models;

namespace Adamantium.Engine.Compiler.Models
{
    public class ModelConverter : PropertyChangedBase
    {
        #region Variables

        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();

        private Boolean convertationInProgress;
        private Boolean convertationFinished = true;

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

        #endregion


        /// <summary>What the imported file had that we could not understand. Empty until an import has run.</summary>
        public IReadOnlyList<String> UnsupportedFeatures { get; private set; } = Array.Empty<String>();

        //The parse is synchronous; the former name ImportFileAsync promised otherwise
        public SceneData ImportFile(string path, CancellationToken cancellationToken = default)
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token, cancellationToken);
            var converter = ModelConverterFactory.GetConverter(path, new ConversionConfig(true));

            ConvertationInProgress = true;
            ConvertationFinished = false;
            try
            {
                return converter.StartConversion(linked.Token);
            }
            catch (OperationCanceledException)
            {
                IsConvertationCancelled = true;
                return null;
            }
            finally
            {
                // In a finally on purpose: when the import threw is exactly when this list matters most
                UnsupportedFeatures = converter.UnsupportedFeatures;
                ConvertationInProgress = false;
                ConvertationFinished = true;
            }
        }

        #region Cancellation

        public Boolean IsConvertationCancelled { get; private set; }

        /// <summary>Asks a running import to stop. Cancellation is checked between libraries and on every geometry,
        /// so it does not take effect instantly - but it does take effect: this method used to only clear flags
        /// while the parse ran happily to the end.</summary>
        public void CancelConvertation()
        {
            cancellation.Cancel();
            IsConvertationCancelled = true;
        }

        #endregion

    }
}
