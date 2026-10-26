#ifndef FISHING_PROFILE_ICE_INCLUDED
#define FISHING_PROFILE_ICE_INCLUDED

// Recesses reveal irregular ledges while never narrowing the reserved 1.5-unit passage.
float ProfileIceWallX(float2 world)
{
    float d=.84-world.y, side=world.x>0 ? .11 : 0;
    float recess=.18*(1-smoothstep(.07,.33,abs(d-.48-side)))+
        .25*(1-smoothstep(.09,.36,abs(d-1.13+side)))+
        .13*(1-smoothstep(.04,.29,abs(d-1.85-side)));
    return .75+recess*smoothstep(0,.20,d);
}

// A quarter-circle rolls the snow lip into the wall; both visible passes share the silhouette.
float ProfileIceDistance(float2 world)
{
    float2 corner=float2(ProfileIceWallX(world)+.14-abs(world.x),world.y-.84);
    return length(max(corner,0))+min(max(corner.x,corner.y),0)-.14;
}

half3 ProfileIceBevelNormal(float2 world,half3 front,float strength)
{
    float2 corner=max(float2(ProfileIceWallX(world)+.14-abs(world.x),world.y-.84),0);
    float slope=(ProfileIceWallX(world+float2(0,.008))-ProfileIceWallX(world-float2(0,.008)))/.016;
    float2 outward=float2(-sign(world.x)*corner.x,corner.y+slope*corner.x);
    outward/=max(length(outward),.0001);
    half bevel=(1-smoothstep(.015,.13,-ProfileIceDistance(world)))*(1-smoothstep(1.8,2.6,.98-world.y));
    return normalize(lerp(front,normalize(half3(outward*.82,.57)),bevel*strength));
}

#endif
