shader_type canvas_item;

uniform float animation_frame = 0.0;
uniform float glitch_strength = 0.0;
uniform float eye_lit = 0.0;

// fragment() 中的内置 COLOR 已经含有原始 UV 的贴图颜色。失真需要重新按
// 偏移 UV 采样，因此在 vertex() 保存尚未乘贴图的节点调制色，避免贴图被乘两次。
varying vec4 colorize_in;

void vertex() {
	colorize_in = COLOR;
}

vec2 mod289(vec2 value) {
	return value - floor(value * (1.0 / 289.0)) * 289.0;
}

vec3 mod289(vec3 value) {
	return value - floor(value * (1.0 / 289.0)) * 289.0;
}

vec3 permute(vec3 value) {
	return mod289((value * 34.0 + 1.0) * value);
}

float snoise(vec2 value) {
	const vec4 c = vec4(0.2113248654, 0.3660254038, -0.5773502692, 0.0243902439);
	vec2 i = floor(value + dot(value, c.yy));
	vec2 x0 = value - i + dot(i, c.xx);
	vec2 i1 = x0.x > x0.y ? vec2(1.0, 0.0) : vec2(0.0, 1.0);
	vec4 x12 = x0.xyxy + c.xxzz;
	x12.xy -= i1;
	i = mod289(i);
	vec3 p = permute(permute(i.y + vec3(0.0, i1.y, 1.0)) + i.x + vec3(0.0, i1.x, 1.0));
	vec3 m = max(0.5 - vec3(dot(x0, x0), dot(x12.xy, x12.xy), dot(x12.zw, x12.zw)), 0.0);
	m *= m;
	m *= m;
	vec3 x = 2.0 * fract(p * c.www) - 1.0;
	vec3 h = abs(x) - 0.5;
	vec3 ox = floor(x + 0.5);
	vec3 a0 = x - ox;
	m *= 1.7928429140 - 0.8537347209 * (a0 * a0 + h * h);
	vec3 g;
	g.x = a0.x * x0.x + h.x * x0.y;
	g.yz = a0.yz * x12.xz + h.yz * x12.yw;
	return 130.0 * dot(m, g);
}

void fragment() {
	// 原始 shader 的时间相位每 256 个 30 FPS tick 回绕；由视觉节点逐 tick
	// 写入，确保雪花、眼睛及随机起始相位使用同一条时间线。
	float phase = mod(animation_frame, 256.0) / 256.0;
	vec2 uv = UV;
	vec2 texture_size = 1.0 / TEXTURE_PIXEL_SIZE;
	float line_noise = snoise(vec2(phase * 1000.0, texture_size.x * 0.5 * UV.y));
	uv.x += line_noise * glitch_strength * 10.0 * TEXTURE_PIXEL_SIZE.x;
	vec4 color = texture(TEXTURE, uv) * colorize_in;
	if (color.a <= 0.0) {
		discard;
	}

	bool blue_mask = abs(color.r - color.g) < (0.5 / 255.0) && color.b > color.r;
	if (blue_mask) {
		vec2 noise_uv = floor((FRAGCOORD.xy + vec2(phase * 10000.0)) / 4.0) * 4.0;
		float noise_value = (snoise(noise_uv) + 0.5) * color.b;
		float ratio = color.b == 0.0 ? 0.0 : color.r / color.b;
		color.rgb = vec3(mix(noise_value, color.b, ratio));
	} else {
		bool green_mask = abs(color.r - color.b) < (0.5 / 255.0) && color.g > color.r;
		if (green_mask) {
			float ratio = color.g == 0.0 ? 0.0 : color.r / color.g;
			color.rgb = vec3(mix(eye_lit * color.g, color.g, ratio));
		}
	}
	COLOR = color;
}
