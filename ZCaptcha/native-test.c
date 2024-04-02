// gcc test.c -o test.o
#include "dlfcn.h"
#include <stdint.h>
#include <stdio.h>
#include <string.h>
#include <unistd.h>
#include <limits.h>
#include <stdlib.h>
#include <time.h>

typedef void (*initialise)(short unsigned int*);
typedef void* (*gen_emoji_captcha)();
typedef void* (*gen_text_captcha)();
typedef void (*dispose_result)();

typedef struct generation_result {
    char* answer;
    char* dummies;
    uint8_t* image_data;
    int image_data_length;
} gen_result;

// gcc test.c -o bin/Release/net9.0/linux-x64/publish/test.o -g
// ./test.o ZCaptcha.so
// gdb --args ./test.o ZCaptcha.so
int main(int argc, char* argv[])
{
    if (argc < 2)
    {
        puts("Error -- Expected library path as first argument!");
        return 1;
    }
    for (int c = 0; c < strlen(argv[1]); c++)
    {
        if (argv[1][c] == '/')
        {
            puts("Error -- Test program should be run from same directory as libary!");
            return 1;
        }
    }
    int cycles = 1;
    if (argc < 3)
    {
        puts("Note - Running one captcha generation cycle (default). Specify cycles as third argument CLI argument to change it.");
    }
    else
    {
        cycles = atoi(argv[2]);
        printf("Running %d captcha generation cycles.\n", cycles);
    }

    clock_t start = clock();
    for (int i = 0; i < cycles; i++)
    {
        char lib_path[PATH_MAX];
        getcwd(lib_path, sizeof(lib_path));
        if (lib_path[strlen(lib_path) - 1] != '/')
        {
            strcat(lib_path, "/");
        }
        strcat(lib_path, argv[1]);

        void* handle = dlopen(lib_path, RTLD_LAZY);
        short unsigned int* font_path = u"Data/NotoColorEmoji-Regular.ttf"; // utf16 str

        initialise init = dlsym(handle, "initialise");
        if (init == NULL)
        {
            puts("Error -- could not load library!");
            return 0;
        }
        init(font_path);
        dispose_result dispose = dlsym(handle, "dispose_result");

        gen_emoji_captcha emoji_gen = dlsym(handle, "gen_emoji_captcha");
        gen_result* result_emoji = emoji_gen();
        printf("emoji_captcha: \n"
            "   answer: '%s',\n"
            "   dummies: [\n%s\n],\n"
            "   image_data_length: %i\n",
            result_emoji->answer,
            result_emoji->dummies,
            result_emoji->image_data_length);
        dispose(result_emoji);

        gen_text_captcha text_gen = dlsym(handle, "gen_text_captcha");
        gen_result* result_text = text_gen();
        printf("text_captcha: \n"
            "   answer: '%s',\n"
            "   dummies: [\n%s\n],\n"
            "   image_data_length: %i\n",
            result_text->answer,
            result_text->dummies,
            result_text->image_data_length);
        dispose(result_text);

        clock_t end = clock();
        double time_taken = ((double)end - start) / CLOCKS_PER_SEC;
        int completed = i + 1;
        printf("Generated %d emoji captchas and %d text captchas in %f seconds.\n", completed, completed, time_taken);
    }
}
