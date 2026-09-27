shader_type canvas_item;

// 原版物品 PNG 烘入了格子底图；按相同 UV 与空格底图比较，只显示物品图案。
uniform sampler2D empty_texture : source_color, filter_nearest;

void fragment() {
	vec4 item = texture(TEXTURE, UV);
	vec3 background = texture(empty_texture, UV).rgb;
	vec3 difference = abs(item.rgb - background);
	float contrast = max(difference.r, max(difference.g, difference.b));
	float opacity = smoothstep(0.035, 0.16, contrast);
	COLOR = vec4(item.rgb, item.a * opacity);
}
