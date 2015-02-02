Shader "2DVLS/Diffuse (2-Sided)" {
    Properties
    {
        _MainTex ("Base (RGB) Trans. (Alpha)", 2D) = "white" { }
    }

    Category
    {
        ZWrite On
        Cull Off
        Lighting Off

        SubShader
        {
		    Pass 
			{
				ColorMask 0
			}

            Pass
            {
           		Blend DstColor DstAlpha

                SetTexture [_MainTex] 
				{ 
                	combine texture
                }             
            }
        } 
    }

	Fallback "Diffuse"
}