using AdamantiumVulkan.Core;
using System;

namespace Adamantium.Engine.Graphics
{
    /// <summary>
    /// Depth-stencil state collection.
    /// </summary>
    public class DepthStencilStatesCollection : StateCollectionBase<DepthStencilState>
    {

        /// <summary>
        /// A built-in state object with disabled depth buffer read/write and write to stencil buffer.
        /// </summary>
        public readonly DepthStencilState None;

        /// <summary>
        /// A built-in state object with default settings for using a depth stencil buffer.
        /// </summary>
        public readonly DepthStencilState DepthEnableLess;

        /// <summary>
        /// A built-in state object with enabled depth, write depth and comparison function = Always.
        /// </summary>
        public readonly DepthStencilState DepthEnableAlways;

        /// <summary>
        /// A built-in state object with LessEqual settings for depth comparison.
        /// Last drawn object will on top if objects has equal depth.
        /// Used for straight depth buffer.
        /// </summary>
        public readonly DepthStencilState DepthEnableLessEqual;

        /// <summary>
        /// A built-in state object with GreaterEqual settings for depth comparison.
        /// Last drawn object will on top if objects has equal depth.
        /// Usually used for inversed depth buffer.
        /// </summary>
        public readonly DepthStencilState DepthEnableGreaterEqual;

        /// <summary>
        /// A built-in state object with Greater settings for depth comparison.
        /// Last drawn object will on top if objects has equal depth.
        /// </summary>
        public readonly DepthStencilState DepthEnableGreater;


        /// <summary>
        /// A built-in state object with Equal settings for depth comparison.
        /// Last drawn object will on top if objects has equal depth.
        /// </summary>
        public readonly DepthStencilState DepthEnableEqual;

        /// <summary>
        /// A built-in state object with settings for enabling a read-only depth stencil buffer.
        /// </summary>
        public readonly DepthStencilState DepthDisable;

        /// <summary>
        /// A built-in state object with settings for enabling a read-only depth stencil buffer and comparison fuction = GreaterEqual.
        /// </summary>
        public readonly DepthStencilState DepthReadGreaterEqual;

        /// <summary>
        /// A built-in state object with settings for enabling a read-only depth stencil buffer and comparison fuction = LessEqual.
        /// </summary>
        public readonly DepthStencilState DepthReadLessEqual;

        /// <summary>
        /// A built-in state object with settings for enabling a read-only depth stencil buffer and comparison fuction = Greater.
        /// </summary>
        public readonly DepthStencilState DepthReadGreater;

        /// <summary>
        /// A built-in state object with settings for enabling a read-only depth stencil buffer and comparison fuction = Less.
        /// </summary>
        public readonly DepthStencilState DepthReadLess;

        /// <summary>
        /// A built-in state object with settings for enabling a read-only depth stencil buffer and comparison fuction = Equal.
        /// </summary>
        public readonly DepthStencilState DepthReadEqual;


        /// <summary>
        /// A built-in state object with settings for enabling a read-only depth stencil buffer and comparison fuction = Always.
        /// </summary>
        public readonly DepthStencilState DepthReadAlways;


        internal DepthStencilStatesCollection()
        {
            None = Add(DepthStencilState.New(nameof(None), false, false, false));
            DepthEnableLess = Add(DepthStencilState.New(nameof(DepthEnableLess), true, true));
            DepthDisable = Add(DepthStencilState.New(nameof(DepthDisable), false, false));
            DepthEnableLessEqual = Add(DepthStencilState.New(nameof(DepthEnableLessEqual), true, true, true, CompareOp.LessOrEqual));
            DepthEnableGreaterEqual = Add(DepthStencilState.New(nameof(DepthEnableGreaterEqual), true, true, true, CompareOp.GreaterOrEqual));
            DepthEnableGreater = Add(DepthStencilState.New(nameof(DepthEnableGreater), true, true, true, CompareOp.Greater));
            DepthEnableEqual = Add(DepthStencilState.New(nameof(DepthEnableEqual), true, true, true, CompareOp.Equal));
            DepthEnableAlways = Add(DepthStencilState.New(nameof(DepthEnableAlways), true, true, true, CompareOp.Always));
            DepthReadGreaterEqual = Add(DepthStencilState.New(nameof(DepthReadGreaterEqual), true, false, true, CompareOp.GreaterOrEqual));
            DepthReadLessEqual = Add(DepthStencilState.New(nameof(DepthReadLessEqual), true, false, true, CompareOp.LessOrEqual));
            DepthReadGreater = Add(DepthStencilState.New(nameof(DepthReadGreater), true, false, true, CompareOp.Greater));
            DepthReadLess = Add(DepthStencilState.New(nameof(DepthReadLess), true, false));
            DepthReadEqual = Add(DepthStencilState.New(nameof(DepthReadEqual), true, false, true, CompareOp.Equal));
            DepthReadAlways = Add(DepthStencilState.New(nameof(DepthReadAlways), true, false, true, CompareOp.Always));
        }
    }
}
