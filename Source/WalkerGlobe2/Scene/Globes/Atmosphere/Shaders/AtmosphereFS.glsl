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
    float fourA = 4.0; // dot(rayDir,rayDir) == 1.0 for normalized rayDir

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

    vec3 rayleighColor = vec3(0.25, 0.45, 0.9);
    float terminatorFactor = clamp(1.0 - abs(sunDot) * 3.0, 0.0, 1.0);
    vec3 color = mix(rayleighColor, vec3(0.7, 0.4, 0.2), terminatorFactor * 0.3);

    float alpha;
    if (tPlanet > 0.0)
    {
        // Over planet: haze across sunlit face + stronger at limb
        vec3 hitPt = eye + rayDir * tPlanet;
        float viewDot = abs(dot(rayDir, normalize(hitPt)));
        float limbEnhance = 1.0 - viewDot;

        // Atmosphere visible on all sides, brighter on sunlit side
        float sunLit = clamp(sunDot * 0.5 + 0.5, 0.25, 1.0);

        // Uniform haze across face
        float baseHaze = 0.08 * sunLit;

        // Extra blue near the limb
        float limbHaze = pow(limbEnhance, 2.0) * 0.18 * sunLit;

        alpha = baseHaze + limbHaze;
        if (alpha < 0.003) discard;
    }
    else
    {
        // Limb glow in space - visible on all sides
        float glow = pow(opticalDepth, 0.4) * 1.0;
        float sunLit = clamp(sunDot * 0.5 + 0.5, 0.25, 1.0);
        alpha = clamp(glow * sunLit, 0.0, 0.95);
    }

    fragmentColor = vec4(color, alpha);
}
