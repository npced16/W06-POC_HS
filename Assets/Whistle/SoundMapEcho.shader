Shader "Whistle/Sound Map Echo"
{
    Properties
    {
        _IdleVisibility ("Idle visibility", Range(0,1)) = 0.025
        _ScanStrength ("Skill reveal", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float4 _SoundOrigins[24];
            float4 _SoundProperties[24];
            float4 _BlockerMin[64], _BlockerMax[64];
            float4 _WaveParameters;
            float _WaveClock, _IdleVisibility, _ScanStrength;
            int _WaveCount, _BlockerCount;
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }
            bool Occluded(float3 origin, float3 target)
            {
                float3 direction = target - origin;
                float3 inverse = (step(0, direction) * 2 - 1) / max(abs(direction), .0001);
                for (int j = 0; j < _BlockerCount; j++)
                {
                    float3 a = (_BlockerMin[j].xyz - origin) * inverse;
                    float3 b = (_BlockerMax[j].xyz - origin) * inverse;
                    float3 entry = min(a, b), exit = max(a, b);
                    float nearHit = max(entry.x, max(entry.y, entry.z));
                    float farHit = min(exit.x, min(exit.y, exit.z));
                    // The receiving surface is visible, geometry beyond the wall is not.
                    if (farHit >= nearHit && farHit > .02 && nearHit < .97) return true;
                }
                return false;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float reveal = 0;
                for (int i = 0; i < _WaveCount; i++)
                {
                    float radius = _SoundProperties[i].x;
                    float distanceToSound = distance(input.positionWS, _SoundOrigins[i].xyz);
                    float timeSinceArrival = _WaveClock - _SoundOrigins[i].w - distanceToSound / max(.1, _WaveParameters.y);
                    if (radius > 0 && distanceToSound <= radius && timeSinceArrival >= 0 && timeSinceArrival < _WaveParameters.z + _WaveParameters.w)
                    {
                        float fade = 1 - saturate((timeSinceArrival - _WaveParameters.z) / max(.01, _WaveParameters.w));
                        // Soften the range boundary instead of drawing a hard visible sphere.
                        float edge = 1 - smoothstep(radius * .8, radius, distanceToSound);
                        if (!Occluded(_SoundOrigins[i].xyz + float3(0,.06,0), input.positionWS)) reveal = max(reveal, fade * edge * _SoundProperties[i].y);
                    }
                }
                float alpha = max(_IdleVisibility, max(_ScanStrength * .88, reveal * .9));
                return half4(1, 1, 1, alpha);
            }
            ENDHLSL
        }
    }
}
