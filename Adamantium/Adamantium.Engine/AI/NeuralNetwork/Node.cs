using System;
using System.Collections.Generic;

namespace Adamantium.Engine.AI.NeuralNetwork
{
   internal class Node
   {
      public List<float> Weights { get; set; }
      public float Error { get; set; }
      public float Output { get; set; }
      public float WeightenedSumOfInputs { get; set; }
      private Random random;

      // constructor
      public Node()
      {
         Weights = new List<float>();
         random = new Random();
      }

      // weights
      public void CreateWeights(int count)
      {
         Weights = new List<float>();

         while (count > 0)
         {
            Weights.Add((float)random.NextDouble());
            --count;
         }
      }
   }
}
