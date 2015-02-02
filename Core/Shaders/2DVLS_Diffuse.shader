Shader "2DVLS/Diffuse" {
    Properties
    {
        _MainTex ("Base (RGB) Trans. (Alpha)", 2D) = "white" { }
    }

    Category
    {
        ZWrite On
        Cull Back
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