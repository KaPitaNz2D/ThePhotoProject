using UnityEngine.ProBuilder;

namespace Overdrive.ProBuilderPlus
{
    public enum UVMode
    {
        /// <summary>
        /// Corresponds to <see cref="Face.manualUV"/> false.<br/>
        /// In this mode, ProBuilder recalculates the UVs after changes to the geometry.<br/>
        /// ProBuilder uses the AutoUnwrapSettings <see cref="Face.uv"/> to calculate the UVs.<br/>
        /// <br/>
        /// Operations by ProBuilder and ProBuilderPlus on UVs target the AutoUnwrapSettings.
        /// </summary>
        Auto,

        /// <summary>
        /// Corresponds to <see cref="Face.manualUV"/> true.
        /// In this mode, ProBuilder does not change the UVs after changes to the geometry.<br/>
        /// <br/>
        /// Operations by ProBuilder and ProBuilderPlus on UVs target the uv0-texture array of the mesh.<br/>
        /// Use <see cref="Face.distinctIndexes"/> to get the indices of Vertices which match the <see cref="ProBuilderMesh.textures"/> list.
        /// </summary>
        Manual
    }
}
