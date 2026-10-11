sampler2D entrada : register(s0);
float3 fila1 : register(c0);
float3 fila2 : register(c1);
float3 fila3 : register(c2);

float4 main(float2 posicion : TEXCOORD) : COLOR
{
    float3 punto = float3(posicion, 1);
    float divisor = dot(fila3, punto);
    float2 uv = float2(dot(fila1, punto), dot(fila2, punto)) / divisor;
    float visible = step(0, uv.x) * step(uv.x, 1) * step(0, uv.y) * step(uv.y, 1);
    return tex2D(entrada, uv) * visible;
}
