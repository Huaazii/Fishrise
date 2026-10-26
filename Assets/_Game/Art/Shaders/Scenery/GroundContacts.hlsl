#ifndef FISHING_GROUND_CONTACTS_INCLUDED
#define FISHING_GROUND_CONTACTS_INCLUDED
float4 _GroundContacts[12];
half SurfaceContactShadow(float2 world, half strength)
{
    half shadow = 0;
    [unroll] for (int n = 0; n < 12; n++)
    {
        float4 contact = _GroundContacts[n];
        float2 d = (world - contact.xy) / max(contact.zw, float2(.001,.001));
        half footprint = saturate(1 - dot(d,d));
        shadow = max(shadow, footprint * footprint * (n < 4 ? .55 : 1));
    }
    // Contact belongs to the snow plane, rather than the vertical blue cut face below it.
    return shadow * strength * 1.7 * smoothstep(.22,.62,world.y);
}
#endif
