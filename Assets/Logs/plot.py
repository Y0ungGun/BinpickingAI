# 필요한 라이브러리 임포트
import pandas as pd
import matplotlib.pyplot as plt

# CSV 파일 읽기
file_path = r"d:\unityworkspace\BinpickingAI\Assets\Logs\rewards.csv"
data = pd.read_csv(file_path)

# x축: deg % 90, y축: Reward1
data['x'] = data['Deg'] % 90
x = data['x']
y = data['Reward']
y = 2 * y -1 

# Scatter plot 그리기
plt.figure(figsize=(10, 6))
plt.scatter(x, y, alpha=0.7, c='blue', edgecolors='k', s=5)
plt.title("Scatter Plot of Quality (x = deg % 90)", fontsize=14)
plt.xlabel("deg % 90", fontsize=12)
plt.ylabel("Quality", fontsize=12)
plt.grid(True, linestyle='--', alpha=0.6)
plt.show()