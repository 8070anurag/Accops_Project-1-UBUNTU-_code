import ctypes
import os
import time

print(f"==========================================")
print(f" DEMO APP — PID: {os.getpid()}")
print(f"==========================================")

# Load the C standard library
libc = ctypes.CDLL("libc.so.6")

# FIX: On 64-bit Linux, we must tell Python that malloc returns a 64-bit pointer!
# Without this, Python cuts the memory address in half, causing a segmentation fault.
libc.malloc.argtypes = [ctypes.c_size_t]
libc.malloc.restype = ctypes.c_void_p
libc.free.argtypes = [ctypes.c_void_p]

# 1. Allocate 500MB in many small chunks so Linux is forced to keep it in the heap cache
print("1. Allocating 500MB directly in C heap...")
CHUNK_SIZE = 50 * 1024 # 50 KB chunks
NUM_CHUNKS = 10000     # 10,000 chunks = 500MB

pointers = []
for i in range(NUM_CHUNKS):
    ptr = libc.malloc(CHUNK_SIZE)
    if ptr:
        # Write one byte so the OS actually provides the physical RAM
        ctypes.memset(ptr, 1, 1)
        pointers.append(ptr)

time.sleep(2)

# 2. Free the memory!
print("2. Freeing 500MB internally...")
for ptr in pointers:
    libc.free(ptr)
    
print("   (Data deleted, but libc is keeping the RAM cached!)")
print(f"   -> Now go run: release-memory {os.getpid()}")
print("==========================================")

count = 1
while True:
    print(f"[{count}] App is still running perfectly! (PID: {os.getpid()})")
    count += 1
    time.sleep(3)
