FROM python:3.9-slim
WORKDIR /app
COPY . .
RUN pip install flask sentence-transformers
CMD ["python", "app.py"]