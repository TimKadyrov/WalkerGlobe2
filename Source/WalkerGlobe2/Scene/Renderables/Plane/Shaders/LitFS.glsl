#version 330

in vec3 v_positionEC;

out vec4 fragmentColor;
uniform vec3 u_color;
uniform float u_alpha;

void main()
{
    // Screen-space normal from eye-space position derivatives
    vec3 dx = dFdx(v_positionEC);
    vec3 dy = dFdy(v_positionEC);
    vec3 normal = normalize(cross(dx, dy));

    // Light from camera direction (headlight)
    vec3 viewDir = normalize(-v_positionEC);
    float diffuse = max(dot(normal, viewDir), 0.0);

    // Specular (Blinn-Phong)
    vec3 halfDir = viewDir; // light == view for headlight
    float spec = pow(max(dot(normal, halfDir), 0.0), 32.0);

    // Ambient + diffuse + specular
    vec3 lit = u_color * (0.25 + 0.55 * diffuse) + vec3(0.3) * spec;

    fragmentColor = vec4(lit, u_alpha);
}
