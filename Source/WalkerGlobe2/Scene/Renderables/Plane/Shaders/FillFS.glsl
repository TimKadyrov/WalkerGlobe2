#version 330
//
// (C) Copyright 2010 Patrick Cozzi and Deron Ohlarik
//
// Distributed under the MIT License.
// See License.txt or http://www.opensource.org/licenses/mit-license.php.
//
             
out vec4 fragmentColor;
uniform vec3 u_color;
uniform float u_alpha;

void main()
{
    fragmentColor = vec4(u_color, u_alpha);
}

/*
#version 330
//
// (C) Copyright 2010 Patrick Cozzi and Deron Ohlarik
//
// Distributed under the MIT License.
// See License.txt or http://www.opensource.org/licenses/mit-license.php.
//
             
out vec4 fragmentColor;
uniform vec3 u_color;
uniform float u_alpha;
in vec2 texCoord;
uniform sampler2D theTexture;

void main()
{
  vec4 texel = texture(theTexture, texCoord);
  if(texel.a < 0.5)
    discard;
  fragmentColor = vec4(u_color, u_alpha);
}*/