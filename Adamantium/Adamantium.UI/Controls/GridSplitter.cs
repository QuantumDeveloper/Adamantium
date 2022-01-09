using System;
using System.Collections.Generic;
using System.Linq;
using Adamantium.UI.Controls.Primitives;
using Adamantium.UI.Input;
using Adamantium.UI.RoutedEvents;

namespace Adamantium.UI.Controls;

public class GridSplitter:Thumb
{
   public static readonly AdamantiumProperty ResizeDirectionProperty =
      AdamantiumProperty.Register(nameof(ResizeDirection),
         typeof (ResizeDirection), typeof (GridSplitter),
         new PropertyMetadata(ResizeDirection.Columns,
            PropertyMetadataOptions.AffectsMeasure | PropertyMetadataOptions.AffectsArrange, ResizeDirectionChanged));

   public static readonly AdamantiumProperty ResizeBehaviorProperty =
      AdamantiumProperty.Register(nameof(ResizeBehavior), typeof (GridResizeBehavior),
         typeof (GridSplitter),
         new PropertyMetadata(GridResizeBehavior.PreviousAndNext,
            PropertyMetadataOptions.AffectsMeasure | PropertyMetadataOptions.AffectsArrange,
            ResizeBehaviorChanged));

   private static void ResizeSchemenChanged(AdamantiumComponent adamantiumObject, AdamantiumPropertyChangedEventArgs adamantiumPropertyChangedEventArgs)
   {
      var splitter = adamantiumObject as GridSplitter;
      if (splitter != null && splitter.grid != null)
      {
         splitter.PrepareGridSplitter();
      }
   }

   public static readonly AdamantiumProperty DeferredResizeEnabledProperty =
      AdamantiumProperty.Register(nameof(DeferredResizeEnabled), typeof (Boolean),
         typeof (GridSplitter), new PropertyMetadata(false));

   public ResizeDirection ResizeDirection
   {
      get => GetValue<ResizeDirection>(ResizeDirectionProperty);
      set => SetValue(ResizeDirectionProperty, value);
   }

   public GridResizeBehavior ResizeBehavior
   {
      get => GetValue<GridResizeBehavior>(ResizeBehaviorProperty);
      set => SetValue(ResizeBehaviorProperty, value);
   }

   public Boolean DeferredResizeEnabled
   {
      get => GetValue<Boolean>(DeferredResizeEnabledProperty);
      set => SetValue(DeferredResizeEnabledProperty, value);
   }

   public GridSplitter()
   { }

   private static void ResizeDirectionChanged(AdamantiumComponent adamantiumObject, AdamantiumPropertyChangedEventArgs e)
   {
      var splitter = adamantiumObject as GridSplitter;
      if (splitter != null && splitter.IsAttachedToVisualTree)
      {
         splitter.PrepareGridSplitter();
      }
   }

   private static void ResizeBehaviorChanged(AdamantiumComponent adamantiumObject, AdamantiumPropertyChangedEventArgs e)
   {
      var splitter = adamantiumObject as GridSplitter;
      if (splitter != null && splitter.IsAttachedToVisualTree)
      {
         splitter.PrepareGridSplitter();
      }
   }

   private double definition1LengthNew;
   private double definition2LengthNew;
   private double prevDelta = 0;

   protected override void OnDragDelta(DragEventArgs e)
   {
      if (isResizeBehaviorValid)
      {
         double delta = ResizeDirection == ResizeDirection.Columns ? e.Change.X : e.Change.Y;
         double max;
         double min;
         GetDeltaConstraints(out min, out max);
         delta = Math.Min(Math.Max(delta, min), max);

         if (prevDelta != delta)
         {
            prevDelta = delta;
            double actualPrev = GetActualLength(definition1);
            double actualNext = GetActualLength(definition2);

            // With floating point operations there may be loss of precision to some degree. Eg. Adding a very 
            // small value to a very large one might result in the small value being ignored. In the following 
            // steps there are two floating point operations viz. actualLength1+delta and actualLength2-delta. 
            // It is possible that the addition resulted in loss of precision and the delta value was ignored, whereas 
            // the subtraction actual absorbed the delta value. This now means that 
            // (definition1LengthNew + definition2LengthNewis) 2 factors of precision away from 
            // (actualLength1 + actualLength2). This can cause a problem in the subsequent drag iteration where 
            // this will be interpreted as the cancellation of the resize operation. To avoid this imprecision we use 
            // make definition2LengthNew be a function of definition1LengthNew so that the precision or the loss 
            // thereof can be counterbalanced.
            definition1LengthNew = actualPrev + delta;
            definition2LengthNew = actualPrev + actualNext - definition1LengthNew;
               
            if (!DeferredResizeEnabled)
            {
               SetLength(definition1LengthNew, definition2LengthNew);
            }
         }
      }
   }

   private void SetLength(double prevDefinitionPixels, double nextDefinitionPixels)
   {
      if (_splitBehaviour == SplitBehaviour.ResizeBoth)
      {
         foreach (var definition in definitions)
         {
            if (definition == definition1)
            {
               SetLengthInStars(definition1, prevDefinitionPixels);
            }
            else if (definition == definition2)
            {
               SetLengthInStars(definition2, nextDefinitionPixels);
            }
            else if (IsStar(definition))
            {
               SetLengthInStars(definition, GetActualLength(definition)); // same size but in stars.
            }
         }
      }
      else if (_splitBehaviour == SplitBehaviour.ResizeFirst)
      {
         SetLengthInPixels(definition1, prevDefinitionPixels);
      }
      else if (_splitBehaviour == SplitBehaviour.ResizeSecond)
      {
         SetLengthInPixels(definition2, nextDefinitionPixels);
      }
      else if (_splitBehaviour == SplitBehaviour.ResizeLeftPlusStar)
      {
         SetLengthInPixels(definition1, prevDefinitionPixels);
         SetLengthInStars(definition2, nextDefinitionPixels);
      }
   }

   protected override void OnDragCompleted(DragCompletedEventArgs e)
   {
      if (isResizeBehaviorValid && DeferredResizeEnabled)
      {
         SetLength(definition1LengthNew, definition2LengthNew);
      }
   }

   private void GetDeltaConstraints(out double min, out double max)
   {
      double definition1Len = GetActualLength(definition1);
      double definition1Min = GetMinLength(definition1);
      double definition1Max = GetMaxLength(definition1);
         
      double definition2Len = GetActualLength(definition2);
      double definition2Min = GetMinLength(definition2);
      double definition2Max = GetMaxLength(definition2);

      if (_splitBehaviour == SplitBehaviour.ResizeBoth)
      {
         // Determine the minimum and maximum the columns can be resized
         min = -Math.Min(definition1Len - definition1Min, definition2Max - definition2Len);
         max = Math.Min(definition1Max - definition1Len, definition2Len - definition2Min);
      }
      else if (_splitBehaviour == SplitBehaviour.ResizeFirst)
      {
         min = definition1Min - definition1Len;
         max = definition1Max - definition1Len;
      }
      else if (_splitBehaviour == SplitBehaviour.ResizeSecond)
      {
         min = definition2Len - definition2Max;
         max = definition2Len - definition2Min;
      }
      else
      {
         min = definition1Min - definition1Len;
         max = Math.Min(definition1Max - definition1Len, definition2Len - definition2Min);
      }
   }

   private double GetActualLength(DefinitionBase definition)
   {
      var columnDefinition = definition as ColumnDefinition;
      return columnDefinition?.ActualWidth ?? ((RowDefinition)definition).ActualHeight;
   }

   private double GetMinLength(DefinitionBase definition)
   {
      var columnDefinition = definition as ColumnDefinition;
      return columnDefinition?.MinWidth ?? ((RowDefinition)definition).MinHeight;
   }

   private double GetMaxLength(DefinitionBase definition)
   {
      var columnDefinition = definition as ColumnDefinition;
      return columnDefinition?.MaxWidth ?? ((RowDefinition)definition).MaxHeight;
   }

   private bool IsStar(DefinitionBase definition)
   {
      var columnDefinition = definition as ColumnDefinition;
      return columnDefinition?.Width.IsStar ?? ((RowDefinition)definition).Height.IsStar;
   }

   private void SetLengthInPixels(DefinitionBase definition, double value)
   {
      if (value < 0)
      {
         //size of the Grid definition could not be less than Zero
         value = 0;
      }
      var columnDefinition = definition as ColumnDefinition;
      if (columnDefinition != null)
      {
         columnDefinition.Width = new GridLength(value, GridUnitType.Pixel);
      }
      else
      {
         ((RowDefinition)definition).Height = new GridLength(value, GridUnitType.Pixel);
      }
   }

   private void SetLengthInStars(DefinitionBase definition, double value)
   {
      if (value < 0)
      {
         //size of the Grid definition could not be less than Zero
         value = 0;
      }
      var columnDefinition = definition as ColumnDefinition;
      if (columnDefinition != null)
      {
         columnDefinition.Width = new GridLength(value, GridUnitType.Star);
      }
      else
      {
         ((RowDefinition)definition).Height = new GridLength(value, GridUnitType.Star);
      }
   }

   protected Grid grid;

   private DefinitionBase definition1;
   private DefinitionBase definition2;
   private List<DefinitionBase> definitions;
   private bool isResizeBehaviorValid = true;

   protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
   {
      base.OnAttachedToVisualTree(e);
      PrepareGridSplitter();
   }

   private void PrepareGridSplitter()
   {
      isResizeBehaviorValid = true;
      var behavior = ResizeBehavior;
      grid = this.GetVisualParent<Grid>();
      if (ResizeDirection == ResizeDirection.Columns)
      {
         Cursor = Cursors.SizeEWE;
         definitions = grid.ColumnDefinitions.Cast<DefinitionBase>().ToList();
         var col = GetValue<int>(Grid.ColumnProperty);
         switch (behavior)
         {
            case GridResizeBehavior.PreviousAndNext:
               if (col <= 0 || col+1> definitions.Count-1)
               {
                  isResizeBehaviorValid = false;
               }
               else
               {
                  definition1 = definitions[col - 1];
                  definition2 = definitions[col + 1];
               }
               break;
            case GridResizeBehavior.PreviousAndCurrent:
               if (col <= 0)
               {
                  isResizeBehaviorValid = false;
               }
               else
               {
                  definition1 = definitions[col - 1];
                  definition2 = definitions[col];
               }
               break;
            case GridResizeBehavior.CurrentAndNext:
               if (col + 1 > definitions.Count - 1)
               {
                  isResizeBehaviorValid = false;
               }
               else
               {
                  definition1 = definitions[col];
                  definition2 = definitions[col + 1];
               }
               break;
         }
      }
      else
      {
         Cursor = Cursors.SizeNS;
         definitions = grid.RowDefinitions.Cast<DefinitionBase>().ToList();
         var row = GetValue<int>(Grid.RowProperty);
         switch (behavior)
         {
            case GridResizeBehavior.PreviousAndNext:
               if (row <= 0 || row+1 > definitions.Count-1)
               {
                  isResizeBehaviorValid = false;
               }
               else
               {
                  definition1 = definitions[row - 1];
                  definition2 = definitions[row + 1];
               }
               break;
            case GridResizeBehavior.PreviousAndCurrent:
               if (row <= 0)
               {
                  isResizeBehaviorValid = false;
               }
               else
               {
                  definition1 = definitions[row - 1];
                  definition2 = definitions[row];
               }
               break;
            case GridResizeBehavior.CurrentAndNext:
               if (row + 1 > definitions.Count - 1)
               {
                  isResizeBehaviorValid = false;
               }
               else
               {
                  definition1 = definitions[row];
                  definition2 = definitions[row + 1];
               }
               break;
         }
      }

      if (isResizeBehaviorValid)
      {
         DefineSplitBehavior();
      }

   }

   private void DefineSplitBehavior()
   {
      bool isStar1 = false;
      bool isStar2 = false;
      if (definition1 is RowDefinition)
      {
         if (((RowDefinition) definition1).Height.IsStar)
         {
            isStar1 = true;
         }
      }
      else
      {
         if (((ColumnDefinition)definition1).Width.IsStar)
         {
            isStar1 = true;
         }
      }

      if (definition2 is RowDefinition)
      {
         if (((RowDefinition)definition2).Height.IsStar)
         {
            isStar2 = true;
         }
      }
      else
      {
         if (((ColumnDefinition)definition2).Width.IsStar)
         {
            isStar2 = true;
         }
      }

      //WPF GridSplitter behaviour
      if (isStar1 && isStar2)
      {
         _splitBehaviour = SplitBehaviour.ResizeBoth;
      }
      else
      {
         _splitBehaviour = !isStar1 ? SplitBehaviour.ResizeFirst : SplitBehaviour.ResizeSecond;
      }
   }

   private SplitBehaviour _splitBehaviour;

   private enum SplitBehaviour
   {
      /// <summary>
      /// This flag means that splitter will resize 2 star definitions
      /// </summary>
      ResizeBoth,

      /// <summary>
      /// This flag means that splitter will resize only first definition if it not star
      /// </summary>
      ResizeFirst,

      /// <summary>
      /// This flag means that splitter will resize only second definition if it not star
      /// </summary>
      ResizeSecond,

      /// <summary>
      /// This flag means that splitter will resize both definitions, but first will be Pixel and Second will be Star
      /// </summary>
      ResizeLeftPlusStar
   }
}