import pandas as pd

# 기존 CSV 파일 경로
input_file = r"d:\unityworkspace\BinpickingAI\Assets\Logs\rewards.csv"

# 새로운 CSV 파일 경로
output_file = r"d:\unityworkspace\BinpickingAI\Assets\Logs\rewards_trimmed.csv"

# CSV 파일 읽기
df = pd.read_csv(input_file, header=None)

# 뒤에서 200,000개 행 추출
df_trimmed = df.tail(10000)

# 새로운 CSV 파일로 저장
df_trimmed.to_csv(output_file, index=False, header=False)

print(f"새로운 CSV 파일이 생성되었습니다: {output_file}")