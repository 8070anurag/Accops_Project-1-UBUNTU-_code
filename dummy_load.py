import time
import os

print(f"PID: {os.getpid()} - Running Dummy Load...")
while True:
    time.sleep(0.5)
    print("*")

