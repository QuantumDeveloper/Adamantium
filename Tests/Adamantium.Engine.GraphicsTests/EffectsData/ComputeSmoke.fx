// Compute smoke test: a shader object writes index+1 through a BDA pointer for a CPU readback to verify. The shader body
// is Slang; the technique block uses the engine's FX syntax.

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
        ComputeShader = ComputeSmokeCS;
    }
}
