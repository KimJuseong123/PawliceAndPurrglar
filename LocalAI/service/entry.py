from app.main import app, config


if __name__ == "__main__":
    import uvicorn

    uvicorn.run(app, host=config.host, port=config.port)
