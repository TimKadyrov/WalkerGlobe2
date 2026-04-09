#version 330

layout(location = og_positionHighVertexLocation) in vec3 positionHigh;
layout(location = og_positionLowVertexLocation) in vec3 positionLow;

uniform mat4 og_modelViewPerspectiveMatrix;
uniform float og_perspectiveFarPlaneDistance;
uniform bool u_logarithmicDepth;
uniform float u_logarithmicDepthConstant;

uniform vec3 og_cameraEyeHigh;
uniform vec3 og_cameraEyeLow;
uniform mat4 og_modelViewPerspectiveMatrixRelativeToEye;
uniform mat4 og_modelViewMatrixRelativeToEye;

out vec3 v_positionEC; // eye-space position for lighting

vec4 applyLogarithmicDepth(
    vec4 clipPosition,
    bool logarithmicDepth,
    float logarithmicDepthConstant,
    float perspectiveFarPlaneDistance)
{
    if (logarithmicDepth)
    {
        clipPosition.z = ((2.0 * log((logarithmicDepthConstant * clipPosition.z) + 1.0) /
                   log((logarithmicDepthConstant * perspectiveFarPlaneDistance) + 1.0)) - 1.0) * clipPosition.w;
    }
    return clipPosition;
}

void main()
{
    vec3 posRelEye = (positionHigh - og_cameraEyeHigh) + (positionLow - og_cameraEyeLow);
    v_positionEC = (og_modelViewMatrixRelativeToEye * vec4(posRelEye, 1.0)).xyz;

    vec4 clipPosition = ogTransformEmulatedDoublePosition(
        positionHigh, positionLow, og_cameraEyeHigh, og_cameraEyeLow,
        og_modelViewPerspectiveMatrixRelativeToEye);
    gl_Position = applyLogarithmicDepth(clipPosition, u_logarithmicDepth, u_logarithmicDepthConstant, og_perspectiveFarPlaneDistance);
}
