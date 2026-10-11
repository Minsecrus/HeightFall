// SPDX-License-Identifier: MIT
// Link and lifetime check; actual GPU rendering is verified in the browser separately.
#include <webgpu/webgpu.h>
#include <cstdio>

int main() {
    WGPUInstanceDescriptor descriptor = {};
    WGPUInstance instance = wgpuCreateInstance(&descriptor);
    if (!instance) return 1;
    wgpuInstanceProcessEvents(instance);
    wgpuInstanceRelease(instance);
    std::puts("Emdawn instance/lifetime smoke test passed");
    return 0;
}
