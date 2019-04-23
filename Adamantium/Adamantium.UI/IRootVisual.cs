﻿using Adamantium.Mathematics;

namespace Adamantium.UI
{
   public interface IRootVisual
   {
      /// <summary>
      /// Converts a point from screen to client coordinates.
      /// </summary>
      /// <param name="point">The point in screen coordinates.</param>
      /// <returns>The point in client coordinates.</returns>
      Point PointToClient(Point point);

      /// <summary>
      /// Converts a point from client to screen coordinates.
      /// </summary>
      /// <param name="point">The point in client coordinates.</param>
      /// <returns>The point in screen coordinates.</returns>
      Point PointToScreen(Point point);
   }
}
