// Compute smoke test for the line-rendering GPU pipeline (Step A3). Validates the whole compute path end to end on
// real hardware: creating + binding + DISPATCHING a compute shader-object, a produce->consume buffer barrier, and
// BDA output - the destination buffer's device address is passed as a uniform and written through a pointer (no UAV
// descriptor, matching the chosen approach). Writes a known pattern (index+1) so a CPU readback can verify it.
//
// SHADER BODY IS SLANG (not HLSL) - first step of the gradual move to Slang. The technique/pass block stays in the
// engine's FX-effect syntax; only the shader code is Slang.
//
// NOTE (GPU-iterate): `(uint*)OutputAddress` is the BDA write via Slang's first-class pointers (bufferDeviceAddress
// is enabled engine-wide). If this exact cast is rejected, fallbacks are `Ptr<uint>(OutputAddress)` or a
// `[[vk::buffer_reference]]` struct.

uint64_t OutputAddress;   // GetDeviceAddress() of the output buffer
uint Count;               // number of uints to write

[shader("compute")]
[numthreads(64, 1, 1)]
void ComputeSmokeCS(uint3 tid : SV_DispatchThreadID)
{
    if (tid.x >= Count)
        return;

    uint* output = (uint*)OutputAddress;
    output[tid.x] = tid.x + 1;   // known pattern -> readback expects output[i] == i + 1
}

technique ComputeSmoke
{
    pass Run
    {
        // Slang (primary backend) IGNORES this and targets spirv_1_6; the parser just requires it non-zero, and it
        // only feeds the DXC fallback. Set to SM 6.6 so even that fallback supports pointers/compute (vs the legacy 5.1).
        EffectName = "ComputeSmoke";
        Profile = 6.6;
        ComputeShader = ComputeSmokeCS;
    }
}
