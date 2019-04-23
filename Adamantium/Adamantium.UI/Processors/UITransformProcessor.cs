﻿using System;
using System.Collections.Generic;
using Adamantium.Engine.Core;
using Adamantium.EntityFramework;
using Adamantium.EntityFramework.Components;
using Adamantium.UI.Media;

namespace Adamantium.UI.Processors
{
    public class UITransformProcessor : UIProcessor
    {
        public UITransformProcessor(EntityWorld world)
            : base(world)
        {
        }

        public override void Update(IGameTime gameTime)
        {
            foreach (var entity in Entities)
            {
                var window = entity.GetComponent<Window>();
                if (window != null)
                {
                    TraverseInDepth(window, ProcessControl);
                }
            }
        }

        private void ProcessControl(IVisual element)
        {
            var control = (FrameworkElement)element;
            if (!control.IsMeasureValid)
            {
                if (!Double.IsNaN(control.Width) && !Double.IsNaN(control.Height))
                {
                    Size s = new Size(control.Width, control.Height);
                    control.Measure(s);
                }
                else
                {
                    control.Measure(Size.Infinity);
                }
            }

            if (!control.IsArrangeValid)
            {
                control.Arrange(new Rect(control.DesiredSize));
            }

            if (control.Parent != null)
            {
                control.Location = control.Bounds.Location + control.Parent.Location;
                control.ClipPosition = control.ClipRectangle.Location + control.Parent.Location;
            }
        }
    }
}