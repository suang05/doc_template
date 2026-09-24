FROM python:3.11-slim

WORKDIR /app

# Install font dependencies for Thai fonts
RUN apt-get update && apt-get install -y --no-install-recommends \
    fontconfig \
    fonts-liberation \
    && rm -rf /var/lib/apt/lists/* \
    && fc-cache -fv

COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY app.py .

EXPOSE 5000

CMD ["gunicorn", "--bind", "0.0.0.0:5000", "--workers", "2", "--timeout", "120", "app:app"]
