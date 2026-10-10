shader_type spatial;
render_mode unshaded, cull_disabled, shadows_disabled;

instance uniform float fill = 1.0;
instance uniform vec4 bar_color : source_color = vec4(1.0);
uniform vec3 back_color : source_color = vec3(0.24, 0.24, 0.24);

void vertex() {
	// 始终正对相机
	MODELVIEW_MATRIX = VIEW_MATRIX * mat4(INV_VIEW_MATRIX[0], INV_VIEW_MATRIX[1], INV_VIEW_MATRIX[2], MODEL_MATRIX[3]);
}

void fragment() {
	ALBEDO = UV.x <= fill ? bar_color.rgb : back_color;
}
