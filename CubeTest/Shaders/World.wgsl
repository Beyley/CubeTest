struct VertexOutputs {
    //The position of the vertex
    @builtin(position) position: vec4<f32>,
    //The texture cooridnate of the vertex
    @location(0) tex_coord: vec2<f32>,
    //The normal of the vertex
    @location(1) normal: vec3<f32>,
    //The fragment position
    @location(2) frag_pos: vec3<f32>
}

struct FragmentInputs {
    @location(0) tex_coord: vec2<f32>,
    @location(1) normal: vec3<f32>,
    @location(2) frag_pos: vec3<f32>
}

struct ModelMatrix {
    model: mat4x4<f32>,
    normal: mat4x4<f32>
}

struct CameraInfo {
    position: vec4<f32>,
    view_matrix: mat4x4<f32>,
}

struct LightInfo {
    position: vec4<f32>,
    color: vec4<f32>,
    ambient: vec4<f32>,
    diffuse: vec4<f32>,
}

@group(1) @binding(0) var<uniform> projection_matrix: mat4x4<f32>;
@group(1) @binding(1) var<uniform> model_matrix: ModelMatrix;
@group(1) @binding(2) var<uniform> camera_info: CameraInfo;
@group(1) @binding(3) var<uniform> light_info: LightInfo;
@group(1) @binding(4) var<uniform> position_offset: vec3<f32>;

@vertex
fn vs_main(
    @location(0) pos: vec3<f32>,
    @location(1) normal: vec3<f32>,
    @location(2) tex_coord: vec2<f32>
) -> VertexOutputs {
    var output: VertexOutputs;

    output.position = projection_matrix * camera_info.view_matrix * model_matrix.model * vec4<f32>(pos, 1.0);
    output.position += vec4<f32>(position_offset, 0.0);
    
    output.tex_coord = tex_coord;
    output.normal = (model_matrix.normal * vec4<f32>(normal, 0.0)).xyz;

    output.frag_pos = vec3<f32>((model_matrix.model * vec4<f32>(pos, 1.0)).xyz);

    return output;
}

//The texture we're sampling
@group(0) @binding(0) var t: texture_2d<f32>;
//The sampler we're using to sample the texture
@group(0) @binding(1) var s: sampler;

@fragment
fn fs_main(input: FragmentInputs) -> @location(0) vec4<f32> {
    var norm: vec3<f32> = normalize(input.normal);
    var light_dir: vec3<f32> = normalize(light_info.position.xyz - input.frag_pos);
    
    var diff: f32 = max(dot(norm, light_dir), 0.0f);
    var diffuse: vec3<f32> = vec3<f32>(diff) * light_info.diffuse.xyz;
    var ambient: vec3<f32> = light_info.ambient.xyz;
    
    var specular_strength: f32 = 0.5f;
    
    var viewDir: vec3<f32> = normalize(camera_info.position.xyz - input.frag_pos);
    var reflectDir: vec3<f32> = reflect(-light_dir, norm);  
    
    var spec: f32 = pow(max(dot(viewDir, reflectDir), 0.0f), light_info.color.w);
    var specular: vec3<f32> = vec3<f32>(specular_strength * spec);  
    
    var result: vec3<f32> = diffuse + ambient + specular;
        
    return vec4<f32>(textureSample(t, s, input.tex_coord).xyz * result, 1.0f);
//    return vec4<f32>(vec3<f32>(input.tex_coord, 0.0f) * result, 1.0f);
//    return vec4<f32>(vec3<f32>(0.5f) * result, 1.0f);
}