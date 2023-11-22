// struct OutputVertex {
//     //The position of the vertex
//     pos: vec3<f32>,
//     //The normal of the vertex
//     normal: vec3<f32>,
//     //The texture coordinate of the vertex
//     tex_coord: vec2<f32>
// }
struct OutputVertex {
    //this is really a vec3<f32>, vec3<f32>, vec2<f32> but padding is bad >:(
    first: vec4<f32>,
    second: vec4<f32>
}

struct Counts {
    index_count: atomic<u32>,
    instance_count: u32,
    first_index: u32,
    base_vertex: u32,
    first_instance: u32,
    vertex_count: atomic<u32>
}

@group(0) @binding(0) var<storage, read_write> output_vertices: array<OutputVertex>;
@group(0) @binding(1) var<storage, read_write> output_indices: array<u32>;

@group(0) @binding(2) var<storage, read> input_blocks: array<u32>;

//atomic counters for the in-use vertices and indices   
@group(0) @binding(3) var<storage, read_write> counts: Counts;

const chunk_size: i32 = 16;
const chunk_size_sq: i32 = 256;
const chunk_size_cu: i32 = 4096;

const chunk_pos_size: i32 = 3;

const chunk_size_u: u32 = 16u;

// TODO: use these varants when https://github.com/gfx-rs/naga/issues/1829 is closed
// var chunk_size_sq: u32 = chunk_size * chunk_size;
// var chunk_size_cu: u32 = chunk_size * chunk_size * chunk_size;

fn get_index(x: i32, y: i32, z: i32) -> i32 {
    return chunk_size_sq * y + chunk_size * z + x + chunk_pos_size;
}

const block_air: u32 = 0u;

@compute
@workgroup_size(4, 4, 4)
fn main(@builtin(global_invocation_id) global_id: vec3<u32>) {
    counts.instance_count = 1u;
    
    // if(global_id.x < 0u || global_id.x >= chunk_size_u
    // || global_id.y < 0u || global_id.y >= chunk_size_u
    // || global_id.z < 0u || global_id.z >= chunk_size_u) {
    //     return;
    // }

    var x = i32(global_id.x);
    var y = i32(global_id.y);
    var z = i32(global_id.z);

    var block_id = input_blocks[get_index(x, y, z)];
    
    //If this block is air, don't do anything
    if(block_id == block_air) {
        return;
    }
    
    //The center of the block
    var pos = vec3<f32>(f32(x) + 0.5, f32(y) + 0.5, f32(z) + 0.5);
    
    // the chunk's offset
    pos += vec3<f32>(f32(input_blocks[0] * u32(chunk_size)), f32(input_blocks[1] * u32(chunk_size)), f32(input_blocks[2] * u32(chunk_size)));

    var xn_visible = false;
    var xp_visible = false;
    var yn_visible = false;
    var yp_visible = false;
    var zn_visible = false;
    var zp_visible = false;

    //Count how many faces are visible
    var visible_faces = 0u;

    //Check if the block is visible from X-positive
    if(x == chunk_size - 1 || input_blocks[get_index(x + 1, y, z)] == block_air) {
        visible_faces++;
        xp_visible = true;
    }

    //Check if the block is visible from X-negative
    if(x == 0 || input_blocks[get_index(x - 1, y, z)] == block_air) {
        visible_faces++;
        xn_visible = true;
    }

    //Check if the block is visible from Y-positive
    if(y == chunk_size - 1 || input_blocks[get_index(x, y + 1, z)] == block_air) {
        visible_faces++;
        yp_visible = true;
    }

    //Check if the block is visible from Y-negative
    if(y == 0 || input_blocks[get_index(x, y - 1, z)] == block_air) {
        visible_faces++;
        yn_visible = true;
    }

    //Check if the block is visible from Z-positive
    if(z == chunk_size - 1 || input_blocks[get_index(x, y, z + 1)] == block_air) {
        visible_faces++;
        zp_visible = true;
    }

    //Check if the block is visible from Z-negative
    if(z == 0 || input_blocks[get_index(x, y, z - 1)] == block_air) {
        visible_faces++;
        zn_visible = true;
    }

    //If the block is completely surrounded by other blocks, don't render it
    if(visible_faces == 0u) {
        return;
    }

    var vertices_per_face = 4u;
    var indices_per_face = 6u;

    //calculate how many vertices and indices are needed by this block
    var vertices_needed = vertices_per_face * visible_faces;
    var indices_needed = indices_per_face * visible_faces;

    var vertex_index = atomicAdd(&counts.vertex_count, vertices_needed);
    var index_index = atomicAdd(&counts.index_count, indices_needed);

    //If xpos is visible, add the vertices and indices for it
    if(xp_visible) {
        //Add the vertices
        // output_vertices[vertex_index + 0u].pos = pos + vec3<f32>(0.5, -0.5, -0.5);
        // output_vertices[vertex_index + 0u].normal = vec3<f32>(1.0, 0.0, 0.0);
        // output_vertices[vertex_index + 0u].tex_coord = vec2<f32>(0.0, 0.0);
        output_vertices[vertex_index + 0u].first = vec4<f32>(pos + vec3<f32>(0.5, -0.5, -0.5), 1.0);
        output_vertices[vertex_index + 0u].second = vec4<f32>(0.0, 0.0, 0.0, 1.0);

        // output_vertices[vertex_index + 1u].pos = pos + vec3<f32>(0.5, -0.5, 0.5);
        // output_vertices[vertex_index + 1u].normal = vec3<f32>(1.0, 0.0, 0.0);
        // output_vertices[vertex_index + 1u].tex_coord = vec2<f32>(1.0, 0.0);
        output_vertices[vertex_index + 1u].first = vec4<f32>(pos + vec3<f32>(0.5, -0.5, 0.5), 1.0);
        output_vertices[vertex_index + 1u].second = vec4<f32>(0.0, 0.0, 1.0, 1.0);
        
        // output_vertices[vertex_index + 2u].pos = pos + vec3<f32>(0.5, 0.5, 0.5);
        // output_vertices[vertex_index + 2u].normal = vec3<f32>(1.0, 0.0, 0.0);
        // output_vertices[vertex_index + 2u].tex_coord = vec2<f32>(1.0, 1.0);
        output_vertices[vertex_index + 2u].first = vec4<f32>(pos + vec3<f32>(0.5, 0.5, 0.5), 1.0);
        output_vertices[vertex_index + 2u].second = vec4<f32>(0.0, 0.0, 1.0, 0.0);

        // output_vertices[vertex_index + 3u].pos = pos + vec3<f32>(0.5, 0.5, -0.5);
        // output_vertices[vertex_index + 3u].normal = vec3<f32>(1.0, 0.0, 0.0);
        // output_vertices[vertex_index + 3u].tex_coord = vec2<f32>(0.0, 1.0);
        output_vertices[vertex_index + 3u].first = vec4<f32>(pos + vec3<f32>(0.5, 0.5, -0.5), 1.0);
        output_vertices[vertex_index + 3u].second = vec4<f32>(0.0, 0.0, 0.0, 0.0);

        //Add the indices
        output_indices[index_index + 0u] = vertex_index + 0u;
        output_indices[index_index + 1u] = vertex_index + 1u;
        output_indices[index_index + 2u] = vertex_index + 2u;
        output_indices[index_index + 3u] = vertex_index + 2u;
        output_indices[index_index + 4u] = vertex_index + 3u;
        output_indices[index_index + 5u] = vertex_index + 0u;

        vertex_index = vertex_index + vertices_per_face;
        index_index = index_index + indices_per_face;
    }

    //If xneg is visible, add the vertices and indices for it
    if(xn_visible) {
        //Add the vertices
        // output_vertices[vertex_index + 0u].pos = pos + vec3<f32>(-0.5, -0.5, 0.5);
        // output_vertices[vertex_index + 0u].normal = vec3<f32>(-1.0, 0.0, 0.0);
        // output_vertices[vertex_index + 0u].tex_coord = vec2<f32>(0.0, 0.0);
        output_vertices[vertex_index + 0u].first = vec4<f32>(pos + vec3<f32>(-0.5, -0.5, 0.5), -1.0);
        output_vertices[vertex_index + 0u].second = vec4<f32>(0.0, 0.0, 0.0, 1.0);
        
        // output_vertices[vertex_index + 1u].pos = pos + vec3<f32>(-0.5, -0.5, -0.5);
        // output_vertices[vertex_index + 1u].normal = vec3<f32>(-1.0, 0.0, 0.0);
        // output_vertices[vertex_index + 1u].tex_coord = vec2<f32>(1.0, 0.0);
        output_vertices[vertex_index + 1u].first = vec4<f32>(pos + vec3<f32>(-0.5, -0.5, -0.5), -1.0);
        output_vertices[vertex_index + 1u].second = vec4<f32>(0.0, 0.0, 1.0, 1.0);
        
        // output_vertices[vertex_index + 2u].pos = pos + vec3<f32>(-0.5, 0.5, -0.5);
        // output_vertices[vertex_index + 2u].normal = vec3<f32>(-1.0, 0.0, 0.0);
        // output_vertices[vertex_index + 2u].tex_coord = vec2<f32>(1.0, 1.0);
        output_vertices[vertex_index + 2u].first = vec4<f32>(pos + vec3<f32>(-0.5, 0.5, -0.5), -1.0);
        output_vertices[vertex_index + 2u].second = vec4<f32>(0.0, 0.0, 1.0, 0.0);

        // output_vertices[vertex_index + 3u].pos = pos + vec3<f32>(-0.5, 0.5, 0.5);
        // output_vertices[vertex_index + 3u].normal = vec3<f32>(-1.0, 0.0, 0.0);
        // output_vertices[vertex_index + 3u].tex_coord = vec2<f32>(0.0, 1.0);
        output_vertices[vertex_index + 3u].first = vec4<f32>(pos + vec3<f32>(-0.5, 0.5, 0.5), -1.0);
        output_vertices[vertex_index + 3u].second = vec4<f32>(0.0, 0.0, 0.0, 0.0);

        //Add the indices
        output_indices[index_index + 0u] = vertex_index + 0u;
        output_indices[index_index + 1u] = vertex_index + 1u;
        output_indices[index_index + 2u] = vertex_index + 2u;
        output_indices[index_index + 3u] = vertex_index + 2u;
        output_indices[index_index + 4u] = vertex_index + 3u;
        output_indices[index_index + 5u] = vertex_index + 0u;

        vertex_index = vertex_index + vertices_per_face;
        index_index = index_index + indices_per_face;
    }

    //If ypos is visible, add the vertices and indices for it
    if(yp_visible) {
        //Add the vertices
        // output_vertices[vertex_index + 0u].pos = pos + vec3<f32>(-0.5, 0.5, 0.5);
        // output_vertices[vertex_index + 0u].normal = vec3<f32>(0.0, 1.0, 0.0);
        // output_vertices[vertex_index + 0u].tex_coord = vec2<f32>(0.0, 0.0);
        output_vertices[vertex_index + 0u].first = vec4<f32>(pos + vec3<f32>(-0.5, 0.5, 0.5), 0.0);
        output_vertices[vertex_index + 0u].second = vec4<f32>(1.0, 0.0, 0.0, 1.0);
        
        // output_vertices[vertex_index + 1u].pos = pos + vec3<f32>(-0.5, 0.5, -0.5);
        // output_vertices[vertex_index + 1u].normal = vec3<f32>(0.0, 1.0, 0.0);
        // output_vertices[vertex_index + 1u].tex_coord = vec2<f32>(1.0, 0.0);
        output_vertices[vertex_index + 1u].first = vec4<f32>(pos + vec3<f32>(-0.5, 0.5, -0.5), 0.0);
        output_vertices[vertex_index + 1u].second = vec4<f32>(1.0, 0.0, 1.0, 1.0);
        
        // output_vertices[vertex_index + 2u].pos = pos + vec3<f32>(0.5, 0.5, -0.5);
        // output_vertices[vertex_index + 2u].normal = vec3<f32>(0.0, 1.0, 0.0);
        // output_vertices[vertex_index + 2u].tex_coord = vec2<f32>(1.0, 1.0);
        output_vertices[vertex_index + 2u].first = vec4<f32>(pos + vec3<f32>(0.5, 0.5, -0.5), 0.0);
        output_vertices[vertex_index + 2u].second = vec4<f32>(1.0, 0.0, 1.0, 0.0);

        // output_vertices[vertex_index + 3u].pos = pos + vec3<f32>(0.5, 0.5, 0.5);
        // output_vertices[vertex_index + 3u].normal = vec3<f32>(0.0, 1.0, 0.0);
        // output_vertices[vertex_index + 3u].tex_coord = vec2<f32>(0.0, 1.0);
        output_vertices[vertex_index + 3u].first = vec4<f32>(pos + vec3<f32>(0.5, 0.5, 0.5), 0.0);
        output_vertices[vertex_index + 3u].second = vec4<f32>(1.0, 0.0, 0.0, 0.0);

        //Add the indices
        output_indices[index_index + 0u] = vertex_index + 0u;
        output_indices[index_index + 1u] = vertex_index + 1u;
        output_indices[index_index + 2u] = vertex_index + 2u;
        output_indices[index_index + 3u] = vertex_index + 2u;
        output_indices[index_index + 4u] = vertex_index + 3u;
        output_indices[index_index + 5u] = vertex_index + 0u;

        vertex_index = vertex_index + vertices_per_face;
        index_index = index_index + indices_per_face;
    }

    //If yneg is visible, add the vertices and indices for it
    if(yn_visible) {
        //Add the vertices
        // output_vertices[vertex_index + 0u].pos = pos + vec3<f32>(-0.5, -0.5, 0.5);
        // output_vertices[vertex_index + 0u].normal = vec3<f32>(0.0, -1.0, 0.0);
        // output_vertices[vertex_index + 0u].tex_coord = vec2<f32>(0.0, 0.0);
        output_vertices[vertex_index + 0u].first = vec4<f32>(pos + vec3<f32>(-0.5, -0.5, 0.5), 0.0);
        output_vertices[vertex_index + 0u].second = vec4<f32>(-1.0, 0.0, 0.0, 1.0);
        
        // output_vertices[vertex_index + 1u].pos = pos + vec3<f32>(0.5, -0.5, 0.5);
        // output_vertices[vertex_index + 1u].normal = vec3<f32>(0.0, -1.0, 0.0);
        // output_vertices[vertex_index + 1u].tex_coord = vec2<f32>(1.0, 0.0);
        output_vertices[vertex_index + 1u].first = vec4<f32>(pos + vec3<f32>(0.5, -0.5, 0.5), 0.0);
        output_vertices[vertex_index + 1u].second = vec4<f32>(-1.0, 0.0, 1.0, 1.0);
        
        // output_vertices[vertex_index + 2u].pos = pos + vec3<f32>(0.5, -0.5, -0.5);
        // output_vertices[vertex_index + 2u].normal = vec3<f32>(0.0, -1.0, 0.0);
        // output_vertices[vertex_index + 2u].tex_coord = vec2<f32>(1.0, 1.0);
        output_vertices[vertex_index + 2u].first = vec4<f32>(pos + vec3<f32>(0.5, -0.5, -0.5), 0.0);
        output_vertices[vertex_index + 2u].second = vec4<f32>(-1.0, 0.0, 1.0, 0.0);

        // output_vertices[vertex_index + 3u].pos = pos + vec3<f32>(-0.5, -0.5, -0.5);
        // output_vertices[vertex_index + 3u].normal = vec3<f32>(0.0, -1.0, 0.0);
        // output_vertices[vertex_index + 3u].tex_coord = vec2<f32>(0.0, 1.0);
        output_vertices[vertex_index + 3u].first = vec4<f32>(pos + vec3<f32>(-0.5, -0.5, -0.5), 0.0);
        output_vertices[vertex_index + 3u].second = vec4<f32>(-1.0, 0.0, 0.0, 0.0);

        //Add the indices
        output_indices[index_index + 0u] = vertex_index + 0u;
        output_indices[index_index + 1u] = vertex_index + 1u;
        output_indices[index_index + 2u] = vertex_index + 2u;
        output_indices[index_index + 3u] = vertex_index + 2u;
        output_indices[index_index + 4u] = vertex_index + 3u;
        output_indices[index_index + 5u] = vertex_index + 0u;

        vertex_index = vertex_index + vertices_per_face;
        index_index = index_index + indices_per_face;
    }

    //If zpos is visible, add the vertices and indices for it
    if(zp_visible) {
        //Add the vertices
        // output_vertices[vertex_index + 0u].pos = pos + vec3<f32>(-0.5, 0.5, 0.5);
        // output_vertices[vertex_index + 0u].normal = vec3<f32>(0.0, 0.0, 1.0);
        // output_vertices[vertex_index + 0u].tex_coord = vec2<f32>(0.0, 0.0);
        output_vertices[vertex_index + 0u].first = vec4<f32>(pos + vec3<f32>(-0.5, 0.5, 0.5), 0.0);
        output_vertices[vertex_index + 0u].second = vec4<f32>(0.0, 1.0, 0.0, 1.0);
        
        // output_vertices[vertex_index + 1u].pos = pos + vec3<f32>(-0.5, -0.5, 0.5);
        // output_vertices[vertex_index + 1u].normal = vec3<f32>(0.0, 0.0, 1.0);
        // output_vertices[vertex_index + 1u].tex_coord = vec2<f32>(1.0, 0.0);
        output_vertices[vertex_index + 1u].first = vec4<f32>(pos + vec3<f32>(-0.5, -0.5, 0.5), 0.0);
        output_vertices[vertex_index + 1u].second = vec4<f32>(0.0, 1.0, 1.0, 1.0);
        
        // output_vertices[vertex_index + 2u].pos = pos + vec3<f32>(0.5, -0.5, 0.5);
        // output_vertices[vertex_index + 2u].normal = vec3<f32>(0.0, 0.0, 1.0);
        // output_vertices[vertex_index + 2u].tex_coord = vec2<f32>(1.0, 1.0);
        output_vertices[vertex_index + 2u].first = vec4<f32>(pos + vec3<f32>(0.5, -0.5, 0.5), 0.0);
        output_vertices[vertex_index + 2u].second = vec4<f32>(0.0, 1.0, 1.0, 0.0);

        // output_vertices[vertex_index + 3u].pos = pos + vec3<f32>(0.5, 0.5, 0.5);
        // output_vertices[vertex_index + 3u].normal = vec3<f32>(0.0, 0.0, 1.0);
        // output_vertices[vertex_index + 3u].tex_coord = vec2<f32>(0.0, 1.0);
        output_vertices[vertex_index + 3u].first = vec4<f32>(pos + vec3<f32>(0.5, 0.5, 0.5), 0.0);
        output_vertices[vertex_index + 3u].second = vec4<f32>(0.0, 1.0, 0.0, 0.0);

        //Add the indices
        output_indices[index_index + 0u] = vertex_index + 0u;
        output_indices[index_index + 1u] = vertex_index + 1u;
        output_indices[index_index + 2u] = vertex_index + 2u;
        output_indices[index_index + 3u] = vertex_index + 2u;
        output_indices[index_index + 4u] = vertex_index + 3u;
        output_indices[index_index + 5u] = vertex_index + 0u;

        vertex_index = vertex_index + vertices_per_face;
        index_index = index_index + indices_per_face;
    }

    //If zneg is visible, add the vertices and indices for it
    if(zn_visible) {
        //Add the vertices
        // output_vertices[vertex_index + 0u].pos = pos + vec3<f32>(-0.5, 0.5, -0.5);
        // output_vertices[vertex_index + 0u].normal = vec3<f32>(0.0, 0.0, -1.0);
        // output_vertices[vertex_index + 0u].tex_coord = vec2<f32>(0.0, 0.0);
        output_vertices[vertex_index + 0u].first = vec4<f32>(pos + vec3<f32>(-0.5, 0.5, -0.5), 0.0);
        output_vertices[vertex_index + 0u].second = vec4<f32>(0.0, -1.0, 0.0, 1.0);
        
        // output_vertices[vertex_index + 1u].pos = pos + vec3<f32>(0.5, 0.5, -0.5);
        // output_vertices[vertex_index + 1u].normal = vec3<f32>(0.0, 0.0, -1.0);
        // output_vertices[vertex_index + 1u].tex_coord = vec2<f32>(1.0, 0.0);
        output_vertices[vertex_index + 1u].first = vec4<f32>(pos + vec3<f32>(0.5, 0.5, -0.5), 0.0);
        output_vertices[vertex_index + 1u].second = vec4<f32>(0.0, -1.0, 1.0, 1.0);
        
        // output_vertices[vertex_index + 2u].pos = pos + vec3<f32>(0.5, -0.5, -0.5);
        // output_vertices[vertex_index + 2u].normal = vec3<f32>(0.0, 0.0, -1.0);
        // output_vertices[vertex_index + 2u].tex_coord = vec2<f32>(1.0, 1.0);
        output_vertices[vertex_index + 2u].first = vec4<f32>(pos + vec3<f32>(0.5, -0.5, -0.5), 0.0);
        output_vertices[vertex_index + 2u].second = vec4<f32>(0.0, -1.0, 1.0, 0.0);

        // output_vertices[vertex_index + 3u].pos = pos + vec3<f32>(-0.5, -0.5, -0.5);
        // output_vertices[vertex_index + 3u].normal = vec3<f32>(0.0, 0.0, -1.0);
        // output_vertices[vertex_index + 3u].tex_coord = vec2<f32>(0.0, 1.0);
        output_vertices[vertex_index + 3u].first = vec4<f32>(pos + vec3<f32>(-0.5, -0.5, -0.5), 0.0);
        output_vertices[vertex_index + 3u].second = vec4<f32>(0.0, -1.0, 0.0, 0.0);

        //Add the indices
        output_indices[index_index + 0u] = vertex_index + 0u;
        output_indices[index_index + 1u] = vertex_index + 1u;
        output_indices[index_index + 2u] = vertex_index + 2u;
        output_indices[index_index + 3u] = vertex_index + 2u;
        output_indices[index_index + 4u] = vertex_index + 3u;
        output_indices[index_index + 5u] = vertex_index + 0u;

        vertex_index = vertex_index + vertices_per_face;
        index_index = index_index + indices_per_face;
    }
}