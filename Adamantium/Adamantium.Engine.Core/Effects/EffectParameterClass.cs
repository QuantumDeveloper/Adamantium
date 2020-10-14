namespace Adamantium.Engine.Core.Effects
{
   /// <summary>	
   /// Values that identify the class of a shader variable.
   /// </summary>	
   /// <remarks>	
   /// The class of a shader variable is not a programming class; the class identifies the variable class such as scalar, vector, object, and so on.
   /// </remarks>	
   /// <msdn-id>ff728733</msdn-id>	
   /// <unmanaged>D3D_SHADER_VARIABLE_CLASS</unmanaged>	
   /// <unmanaged-short>D3D_SHADER_VARIABLE_CLASS</unmanaged-short>	
   public enum EffectParameterClass : byte
   {
      /// <summary>	
      /// <dd> <p>The shader variable is a scalar.</p> </dd>	
      /// </summary>	
      /// <msdn-id>ff728733</msdn-id>	
      /// <unmanaged>D3D_SVC_SCALAR</unmanaged>	
      /// <unmanaged-short>D3D_SVC_SCALAR</unmanaged-short>	
      Scalar = unchecked((int)0),

      /// <summary>	
      /// <dd> <p>The shader variable is a vector.</p> </dd>	
      /// </summary>	
      /// <msdn-id>ff728733</msdn-id>	
      /// <unmanaged>D3D_SVC_VECTOR</unmanaged>	
      /// <unmanaged-short>D3D_SVC_VECTOR</unmanaged-short>	
      Vector = unchecked((int)1),

      /// <summary>	
      /// <dd> <p>The shader variable is a row-major matrix.</p> </dd>	
      /// </summary>	
      /// <msdn-id>ff728733</msdn-id>	
      /// <unmanaged>D3D_SVC_MATRIX_ROWS</unmanaged>	
      /// <unmanaged-short>D3D_SVC_MATRIX_ROWS</unmanaged-short>	
      MatrixRows = unchecked((int)2),

      /// <summary>	
      /// <dd> <p>The shader variable is a column-major matrix.</p> </dd>	
      /// </summary>	
      /// <msdn-id>ff728733</msdn-id>	
      /// <unmanaged>D3D_SVC_MATRIX_COLUMNS</unmanaged>	
      /// <unmanaged-short>D3D_SVC_MATRIX_COLUMNS</unmanaged-short>	
      MatrixColumns = unchecked((int)3),

      /// <summary>	
      /// <dd> <p>The shader variable is an object.</p> </dd>	
      /// </summary>	
      /// <msdn-id>ff728733</msdn-id>	
      /// <unmanaged>D3D_SVC_OBJECT</unmanaged>	
      /// <unmanaged-short>D3D_SVC_OBJECT</unmanaged-short>	
      Object = unchecked((int)4),

      /// <summary>	
      /// <dd> <p>The shader variable is a structure.</p> </dd>	
      /// </summary>	
      /// <msdn-id>ff728733</msdn-id>	
      /// <unmanaged>D3D_SVC_STRUCT</unmanaged>	
      /// <unmanaged-short>D3D_SVC_STRUCT</unmanaged-short>	
      Struct = unchecked((int)5),

      /// <summary>	
      /// <dd> <p>The shader variable is a class.</p> </dd>	
      /// </summary>	
      /// <msdn-id>ff728733</msdn-id>	
      /// <unmanaged>D3D_SVC_INTERFACE_CLASS</unmanaged>	
      /// <unmanaged-short>D3D_SVC_INTERFACE_CLASS</unmanaged-short>	
      InterfaceClass = unchecked((int)6),

      /// <summary>	
      /// <dd> <p>The shader variable is an interface.</p> </dd>	
      /// </summary>	
      /// <msdn-id>ff728733</msdn-id>	
      /// <unmanaged>D3D_SVC_INTERFACE_POINTER</unmanaged>	
      /// <unmanaged-short>D3D_SVC_INTERFACE_POINTER</unmanaged-short>	
      InterfacePointer = unchecked((int)7),
   }
}
