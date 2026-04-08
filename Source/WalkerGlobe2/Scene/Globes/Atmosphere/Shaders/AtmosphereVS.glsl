#version 330

layout(location = og_positionVertexLocation) in vec4 position;
out vec3 worldPosition;

uniform mat4 og_modelViewPerspectiveMatrix;

void main()
{
    gl_Position = og_modelViewPerspectiveMatrix * position;
    worldPosition = position.xyz;
}
