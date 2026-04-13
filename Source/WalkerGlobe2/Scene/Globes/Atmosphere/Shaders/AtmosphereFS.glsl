#version 330

in vec3 worldPosition;
out vec4 fragmentColor;

uniform vec3 og_cameraEye;
uniform vec3 og_sunPosition;
uniform mat4 og_inverseModelMatrix;

uniform vec3 u_globeRadiiSquared;
uniform float u_atmosphereRadiusSquared;

void main()
{
    vec3 eye = (og_inverseModelMatrix * vec4(og_cameraEye, 1.0)).xyz;
    vec3 sunDir = normalize((og_inverseModelMatrix * vec4(og_sunPosition, 0.0)).xyz);
    vec3 rayDir = normalize(worldPosition - eye);

    float b = 2.0 * dot(eye, rayDir);
    float bSq = b * b;
    float fourA = 4.0;

    // Atmosphere shell intersection
    float cAtm = dot(eye, eye) - u_atmosphereRadiusSquared;
    float discAtm = bSq - fourA * cAtm;
    if (discAtm < 0.0) discard;

    float sqrtDiscAtm = sqrt(discAtm);
    float t0 = (-b - sqrtDiscAtm) * 0.5;
    float t1 = (-b + sqrtDiscAtm) * 0.5;
    if (t1 < 0.0) discard;

    // Planet intersection
    float meanRadiusSq = (u_globeRadiiSquared.x + u_globeRadiiSquared.y + u_globeRadiiSquared.z) / 3.0;
    float cPln = dot(eye, eye) - meanRadiusSq;
    float discPln = bSq - fourA * cPln;

    float tPlanet = -1.0;
    if (discPln > 0.0)
    {
        float tp = (-b - sqrt(discPln)) * 0.5;
        if (tp > 0.0) tPlanet = tp;
    }

    float tEnter = max(t0, 0.0);
    float tExit = (tPlanet > 0.0) ? min(t1, tPlanet) : t1;
    if (tExit <= tEnter) discard;

    float pathLength = tExit - tEnter;
    float maxPath = 2.0 * sqrt(u_atmosphereRadiusSquared - meanRadiusSq);
    float opticalDepth = clamp(pathLength / maxPath, 0.0, 1.0);

    // Lighting at sample point
    vec3 samplePt = eye + rayDir * ((tEnter + tExit) * 0.5);
    vec3 sNormal = normalize(samplePt);
    float sunDot = dot(sNormal, sunDir);

    // Google Earth style colors
    vec3 deepBlue = vec3(0.40, 0.65, 1.0);
    vec3 limbWhite = vec3(0.70, 0.85, 1.0);
    vec3 termWarm  = vec3(0.85, 0.55, 0.25);  // subtle warm band at terminator

    // Smooth day/night factor: wide S-curve across the terminator
    // sunDot range [-0.6, 0.6] maps to [0, 1] with smooth easing
    float dayFactor = smoothstep(-0.6, 0.6, sunDot);

    // Terminator band: very subtle warm hint at the boundary
    float termBand = exp(-sunDot * sunDot / 0.08);  // wide, soft Gaussian

    float alpha;
    vec3 color;

    if (tPlanet > 0.0)
    {
        // Over planet surface
        vec3 hitPt = eye + rayDir * tPlanet;
        float viewDot = abs(dot(rayDir, normalize(hitPt)));
        float limbEnhance = 1.0 - viewDot;

        // Illumination: smooth transition, night side retains faint glow
        float sunLit = mix(0.08, 1.0, dayFactor);

        // Very subtle uniform haze
        float baseHaze = 0.02 * sunLit;

        // Concentrated limb glow
        float limbGlow = pow(limbEnhance, 4.0) * 0.35 * sunLit;

        // Extra-bright thin edge right at the limb
        float limbEdge = pow(limbEnhance, 8.0) * 0.25 * sunLit;

        alpha = baseHaze + limbGlow + limbEdge;

        // Color: blue-white, with subtle warm tint at the terminator
        float whiteShift = pow(limbEnhance, 3.0);
        color = mix(deepBlue, limbWhite, whiteShift);
        color = mix(color, termWarm, termBand * 0.12 * pow(limbEnhance, 2.0));

        if (alpha < 0.002) discard;
    }
    else
    {
        // Space glow around the limb
        float glow = pow(opticalDepth, 1.8) * 1.6;
        float edge = pow(opticalDepth, 4.0) * 2.0;

        // Smooth transition — night side limb still faintly visible
        float sunLit = mix(0.06, 1.0, dayFactor);
        alpha = clamp((glow + edge) * sunLit, 0.0, 0.85);

        // Color: white at thin edge, blue at thicker parts, warm at terminator
        color = mix(limbWhite, deepBlue, pow(opticalDepth, 0.5));
        color = mix(color, termWarm, termBand * 0.1);

        if (alpha < 0.002) discard;
    }

    fragmentColor = vec4(color, alpha);
}
