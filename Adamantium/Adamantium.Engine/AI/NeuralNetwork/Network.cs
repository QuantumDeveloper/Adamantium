using System;
using System.Collections.Generic;

namespace Adamantium.Engine.AI.NeuralNetwork
{
   class Network
   {
      public List<Layer> Layers { get; set; }
      public float TeachingSpeed { get; set; }
      public double PassCount { get; set; }
      public bool IsLearning { get; set; }
      public List<bool> TeachingData { get; set; }
      public int InputsCount { get; set; }
      public int OutputsCount { get; set; }

      // constructor
      public Network()
      {
         Layers = new List<Layer>();
         IsLearning = false;
         TeachingData = new List<bool>();
      }

      // initialization of network
      public void InitializeNetwork()
      {
         InputsCount = 0;
         OutputsCount = 0;

         if (Layers.Count > 1)
         {
            OutputsCount = Layers[Layers.Count - 1].Count;
         }

         if (Layers.Count > 0)
         {
            InputsCount = Layers[0].Count;
         }

         for (int layer = 1; layer < Layers.Count; ++layer) // zero layer is Input layer, no weight operations allowed
         {
            foreach (Node node in Layers[layer])
            {
               node.CreateWeights(Layers[layer - 1].Count);
            }
         }
      }

      // activation function + derivative - common for all nodes
      private float ActivationFunction(float x)
      {
         return (float)(1 / (1 + Math.Exp(-x)));
      }
      private float DerivativeOfActivationFunction(float x)
      {
         float func = ActivationFunction(x);

         return (func * (1 - func));
      }

      // input / output managing
      public void SetInputs(List<bool> inputs)
      {
         for (int i = 0; i < inputs.Count; ++i)
         {
            Layers[0][i].Output = inputs[i] ? 1.0f : 0.0f;
         }
      }
      public void GetOutputs(ref List<float> result)
      {
         result = new List<float>();

         foreach (Node node in Layers[Layers.Count - 1])
         {
            result.Add(node.Output);
         }
      }
      public void GetOutputs(ref List<bool> result, float trueStartsFrom = 0.9f) // trueStartsFrom - what float value between 0.0 and 1.0 should be considered as True
      {
         result = new List<bool>();

         foreach (Node node in Layers[Layers.Count - 1])
         {
            bool output = (node.Output >= trueStartsFrom) ? true : false;
            result.Add(output);
         }
      }

      // computing
      private float ComputeWeightenedSumOfInputs(int layerNumber, int nodeNumber)
      {
         float weightenedSum = 0;
         int previousLayerNumber = layerNumber - 1;

         for (int i = 0; i < Layers[previousLayerNumber].Count; ++i) // take previous layer's node's outputs
         {
            weightenedSum += Layers[previousLayerNumber][i].Output * Layers[layerNumber][nodeNumber].Weights[i];
         }

         return weightenedSum;
      }
      private float ComputeError(int layerNumber, int nodeNumber)
      {
         float error = 0;
         int nextLayerNumber = layerNumber + 1;

         if (nextLayerNumber < Layers.Count)
         {
            for (int i = 0; i < Layers[nextLayerNumber].Count; ++i) // take next layer's node's errors and weights
            {
               error += Layers[nextLayerNumber][i].Weights[nodeNumber] * Layers[nextLayerNumber][i].Error;
            }
         }
         else // for last layer we use TeachingData for error computing
         {
            float teachingValue = TeachingData[nodeNumber] ? 1.0f : 0.0f;
            error = teachingValue - Layers[layerNumber][nodeNumber].Output;
         }

         return error;
      }
      private void ComputeFixedWeights(int layerNumber, int nodeNumber)
      {
         int previousLayerNumber = layerNumber - 1;
         float error = Layers[layerNumber][nodeNumber].Error;
         float weightenedSumOfInputs = Layers[layerNumber][nodeNumber].WeightenedSumOfInputs;

         for (int i = 0; i < Layers[previousLayerNumber].Count; ++i) // take previous layer's node's outputs
         {
            float oldWeight = Layers[layerNumber][nodeNumber].Weights[i];            
            float input = Layers[previousLayerNumber][i].Output;

            Layers[layerNumber][nodeNumber].Weights[i] = oldWeight + TeachingSpeed * error * DerivativeOfActivationFunction(weightenedSumOfInputs) * input;
         }
      }

      // passings
      private void ForwardPass()
      {
         for (int layer = 1; layer < Layers.Count; ++layer) // zero layer is input layer, output value is already present for its nodes
         {
            for (int node = 0; node < Layers[layer].Count; ++node)
            {
               Layers[layer][node].WeightenedSumOfInputs = ComputeWeightenedSumOfInputs(layer, node);
               Layers[layer][node].Output = ActivationFunction(Layers[layer][node].WeightenedSumOfInputs);
            }
         }
      }
      private void ErrorComputingPass()
      {
         for (int layer = Layers.Count - 1; layer > 0; --layer) // no error computing for zero layer allowed
         {
            for (int node = 0; node < Layers[layer].Count; ++node)
            {
               Layers[layer][node].Error = ComputeError(layer, node);
            }
         }
      }
      private void WeightsFixingPass()
      {
         for (int layer = 1; layer < Layers.Count; ++layer) // zero layer is Input layer, no weight operations allowed
         {
            for (int node = 0; node < Layers[layer].Count; ++node)
            {
               ComputeFixedWeights(layer, node);
            }
         }
      }
      public void SinglePass()
      {
         ForwardPass();

         if (IsLearning == true)
         {
            ErrorComputingPass();
            WeightsFixingPass();
         }

         ++PassCount;
      }

      // helper functions
      public void AddLayers(int count)
      {
         PassCount = 0; // any modification of the network results in reset of all weights

         while (count > 0)
         {
            Layers.Add(new Layer());
            --count;
         }
      }
      public void AddNodesToLayer(int nodesCount, int layerNumber)
      {
         PassCount = 0; // any modification of the network results in reset of all weights

         while (nodesCount > 0)
         {
            Layers[layerNumber].Add(new Node());
            --nodesCount;
         }
      }
   }
}
