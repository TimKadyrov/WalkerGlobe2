#version 330
//
// (C) Copyright 2010 Patrick Cozzi and Deron Ohlarik
//
// Distributed under the MIT License.
// See License.txt or http://www.opensource.org/licenses/mit-license.php.
//

layout(location = og_positionVertexLocation) in vec4 position;
out vec3 worldPosition;
out vec3 positionToLight;
out vec3 positionToEye;

uniform mat4 og_modelViewPerspectiveMatrix;
uniform vec3 og_cameraEye;
uniform vec3 og_cameraLightPosition;

uniform mat4 og_inverseModelMatrix;

void main()
{
    gl_Position = og_modelViewPerspectiveMatrix * position;

    // Body-fixed frame for texture coordinates and surface normal
    worldPosition = position.xyz;

    // Transform camera/light to body-fixed frame for lighting
    vec3 eyeLocal = (og_inverseModelMatrix * vec4(og_cameraEye, 1.0)).xyz;
    vec3 lightLocal = (og_inverseModelMatrix * vec4(og_cameraLightPosition, 1.0)).xyz;
    positionToLight = lightLocal - position.xyz;
    positionToEye = eyeLocal - position.xyz;
}
